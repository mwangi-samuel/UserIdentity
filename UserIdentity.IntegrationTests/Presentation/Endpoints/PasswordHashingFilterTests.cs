using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PolyzenKit.Presentation.Settings;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class PasswordHashingFilterTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string LoginUri = "/api/v1/user/login";
    private const string ClientAddressHeader = "X-Test-Client-Address";
    private const string FirstClientAddress = "203.0.113.1";
    private const string SecondClientAddress = "203.0.113.2";

    private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;
    private readonly BlockingPasswordHasher _passwordHasher = new();

    [Fact]
    public async Task Login_While_Another_Is_Hashing_Gets_Too_Many_Requests_And_Reads_Still_Answer()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        await using var factory = CreateFactory(queueLimit: 0);
        using var httpClient = factory.CreateClient();

        var firstLogin = LogInAsync(httpClient, FirstClientAddress);

        await _passwordHasher.Entered.Task.WaitAsync(_waitLimit, TestContext.Current.CancellationToken);

        // Act
        var refusedLogin = await LogInAsync(httpClient, FirstClientAddress);
        var read = await httpClient.SendNoAuthRequestAsync(HttpMethod.Get, "/api/v1/jwks/keys");

        _passwordHasher.Release();

        var firstLoginResponse = await firstLogin;
        var laterLogin = await LogInAsync(httpClient, FirstClientAddress);

        // Assert
        var responseString = await refusedLogin.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, refusedLogin.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Too Many Requests", $"{jsonObject["error"]?["message"]}");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, laterLogin.StatusCode);
    }

    [Fact]
    public async Task Requests_Over_Their_Rate_Limit_Take_No_Queue_Place_From_Another_Client()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        await using var factory = CreateFactory(queueLimit: 1, permitLimit: 1);
        using var httpClient = factory.CreateClient();

        var firstLogin = LogInAsync(httpClient, FirstClientAddress);

        await _passwordHasher.Entered.Task.WaitAsync(_waitLimit, TestContext.Current.CancellationToken);

        // Act
        var overLimit = await Task.WhenAll(
                LogInAsync(httpClient, FirstClientAddress),
                LogInAsync(httpClient, FirstClientAddress),
                LogInAsync(httpClient, FirstClientAddress)
            )
            .WaitAsync(_waitLimit, TestContext.Current.CancellationToken);

        var queuedLogin = LogInAsync(httpClient, SecondClientAddress);

        _passwordHasher.Release();

        // Assert
        Assert.All(overLimit, response => Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode));
        Assert.Equal(HttpStatusCode.OK, (await firstLogin).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await queuedLogin).StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(int queueLimit, int? permitLimit = null) =>
        _testingWebAppFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{nameof(PasswordHashingSettings)}:{nameof(PasswordHashingSettings.MaxConcurrent)}", "1");
            builder.UseSetting($"{nameof(PasswordHashingSettings)}:{nameof(PasswordHashingSettings.QueueLimit)}", $"{queueLimit}");

            if (permitLimit is not null)
            {
                builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "true");
                builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.PermitLimit)}", $"{permitLimit}");
                builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.WindowSeconds)}", "60");
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPasswordHasher<IdentityUser>>(_passwordHasher);
                services.AddSingleton<IStartupFilter, ClientAddressStartupFilter>();
            });
        });

    private static Task<HttpResponseMessage> LogInAsync(HttpClient httpClient, string clientAddress)
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, LoginUri);

        httpRequest.Headers.Add(ClientAddressHeader, clientAddress);
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(
            new { UserName = UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );

        return httpClient.SendAsync(httpRequest);
    }

    private sealed class BlockingPasswordHasher : IPasswordHasher<IdentityUser>
    {
        private readonly PasswordHasher<IdentityUser> _passwordHasher = new();
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _released.TrySetResult();

        public string HashPassword(IdentityUser user, string password) => _passwordHasher.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(IdentityUser user, string hashedPassword, string providedPassword)
        {
            Entered.TrySetResult();
            _released.Task.Wait(_waitLimit);

            return _passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    // TestServer has no client address, which the per-IP rate limit needs
    private sealed class ClientAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(
                    (HttpContext context, RequestDelegate nextMiddleware) =>
                    {
                        if (IPAddress.TryParse(context.Request.Headers[ClientAddressHeader], out var clientAddress))
                            context.Connection.RemoteIpAddress = clientAddress;

                        return nextMiddleware(context);
                    }
                );

                next(app);
            };
    }
}
