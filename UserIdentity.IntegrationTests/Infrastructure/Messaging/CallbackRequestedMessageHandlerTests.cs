using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Utilities;
using UserIdentity.Common.Enums;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Infrastructure.Messaging;

public class CallbackRequestedMessageHandlerTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string RecaptchaToken = "callback-recaptcha-token";
    private const string ServiceName = "testservice";

    private readonly StubHttpServer _stubHttpServer = testingWebAppFactory.StubHttpServer;

    [Fact]
    public async Task Sign_Up_For_A_Service_Sends_One_Signed_Callback()
    {
        // Arrange
        SetUpStubHttpServer(failFirstCallback: false);

        // Act
        var userId = await RegisterAsync(TestingWebAppFactory.CallbackAppName, RequestSource.Api);

        // Assert
        var callbacks = await WaitForCallbacksAsync(1);
        var callback = Assert.Single(callbacks);

        using var body = JsonDocument.Parse(callback.Body);
        Assert.Equal(userId, body.RootElement.GetProperty("userContent").GetProperty("userIdentityId").GetString());
        Assert.Equal("Create", body.RootElement.GetProperty("eventType").GetString());

        var timestamp = callback.Headers[CustomHeaderNames.Timestamp];
        var signature = callback.Headers[CustomHeaderNames.Signature];
        Assert.True(
            HmacSignature.IsValid(
                TestingWebAppFactory.CallbackAppSecretKey,
                timestamp,
                Encoding.UTF8.GetBytes(callback.Body),
                signature,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5)
            )
        );
        Assert.Equal("TestApiKey-For-Local-Use-Only-0123456789", callback.Headers["X-Api-Key"]);
    }

    [Fact]
    public async Task A_Failed_Callback_Is_Sent_Again()
    {
        // Arrange
        SetUpStubHttpServer(failFirstCallback: true);

        // Act
        await RegisterAsync(TestingWebAppFactory.CallbackAppName, RequestSource.Api);

        // Assert
        var callbacks = await WaitForCallbacksAsync(2);

        Assert.Equal(2, callbacks.Count);
    }

    [Fact]
    public async Task Sign_Up_Through_The_Apps_UI_Adds_No_Callback_Message()
    {
        // Arrange
        SetUpStubHttpServer(failFirstCallback: false);

        // Act
        var userId = await RegisterAsync(TestingWebAppFactory.CallbackAppName, RequestSource.UI);

        // Assert
        await AssertNoCallbackAsync(userId);
    }

    [Fact]
    public async Task Sign_Up_For_An_App_Without_A_Callback_Url_Adds_No_Callback_Message()
    {
        // Arrange
        SetUpStubHttpServer(failFirstCallback: false);

        // Act
        var userId = await RegisterAsync(RegisteredApp.AppName, RequestSource.Api);

        // Assert
        await AssertNoCallbackAsync(userId);
    }

    private async Task AssertNoCallbackAsync(string userId)
    {
        Assert.NotNull(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userId));

        await Eventually.HoldsAsync(() => TestDbHelper.CountOutboxMessages() == 0);

        Assert.Null(await NatsTestHelper.FindMessageAsync(NatsTestHelper.CallbackRequestedSubject, userId, TimeSpan.FromSeconds(2)));
        Assert.Empty(Callbacks());
    }

    private void SetUpStubHttpServer(bool failFirstCallback)
    {
        var failNextCallback = failFirstCallback;

        _stubHttpServer.Reset();

        _stubHttpServer.OnPost("/siteverify", _ => Results.Json(new { success = true, score = 0.9 }));

        _stubHttpServer.OnPost(
            TestingWebAppFactory.CallbackPath,
            _ => Interlocked.Exchange(ref failNextCallback, false) ? Results.InternalServerError() : Results.Ok()
        );
    }

    private async Task<string> RegisterAsync(string appName, RequestSource requestSource)
    {
        var uri = requestSource == RequestSource.Api ? "/api/v1/user/register-for-service" : "/api/v1/user/register";
        var request = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Remove("X-App-Name");
        request.Headers.Add("X-App-Name", appName);

        if (requestSource == RequestSource.Api)
            request.AddAuthHeader(CreateServiceToken(appName));

        request.Content = SerDeHelper.ConvertToHttpContent(
            new
            {
                UserSettingHelper.FirstName,
                UserSettingHelper.UserName,
                UserSettingHelper.UserEmail,
                UserSettingHelper.UserPassword,
                GoogleRecaptchaToken = RecaptchaToken,
            }
        );

        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return SerDeHelper.Deserialize<JObject>(responseString)?["user"]?["id"]?.ToString()
            ?? throw new InvalidOperationException("The sign-up returned no user id");
    }

    private string CreateServiceToken(string appName)
    {
        var jwtTokenHandler = ServiceProvider.GetRequiredService<IJwtTokenHandler>();
        var scope = jwtTokenHandler.GenerateScopeClaim("useridentity-user", ScopeActions.Create);

        (var token, _) = jwtTokenHandler.CreateToken(ServiceName, ServiceName, appName, [KnownRoles.InternalService], [scope.Value]);

        return token;
    }

    private List<StubHttpRequest> Callbacks() =>
        [.. _stubHttpServer.Requests.Where(request => request.Path == TestingWebAppFactory.CallbackPath)];

    private async Task<List<StubHttpRequest>> WaitForCallbacksAsync(int count)
    {
        await Eventually.HoldsAsync(() => Callbacks().Count >= count);

        return Callbacks();
    }
}
