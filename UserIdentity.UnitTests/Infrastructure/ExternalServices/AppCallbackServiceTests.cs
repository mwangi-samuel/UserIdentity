using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Infrastructure.ExternalServices;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Infrastructure.ExternalServices;

using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.ExternalServices;

public class AppCallbackServiceTests
{
  private const string AppSecretKey = "S3TAG00dSeC8E7";

  private static readonly DateTimeOffset Now = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

  private readonly IHttpClientHelper _httpClientHelper;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly IHttpContextAccessor _httpContextAccessor;
  private readonly IOptions<RoleSettings> _roleSettingsOptions;
  private readonly FakeTimeProvider _timeProvider;
  private readonly HttpRequestMessage _requestMessage;

  public AppCallbackServiceTests()
  {
    _httpClientHelper = A.Fake<IHttpClientHelper>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    _httpContextAccessor = A.Fake<IHttpContextAccessor>();
    _httpContextAccessor.HttpContext = new DefaultHttpContext();
    _roleSettingsOptions = Options.Create(new RoleSettings
    {
      ServiceName = "UserIdentity",
      AdminRoles = string.Empty,
      DefaultRole = "user"
    });
    _timeProvider = new FakeTimeProvider(Now);

    _requestMessage = new HttpRequestMessage();
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.CreateHttpRequestMessage))
      .WithReturnType<HttpRequestMessage>()
      .Returns(_requestMessage);

    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task>()
      .Returns(Task.CompletedTask);
  }

  [Fact]
  public async Task SendCallbackRequestAsync_ForwardsRequestToHttpClientHelper()
  {
    var service = GetAppCallbackService();

    await service.SendCallbackRequestAsync(GetApp(), GetUser(), TestContext.Current.CancellationToken);

    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.CreateHttpRequestMessage))
      .MustHaveHappenedOnceExactly();
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync)
        && call.GetArgument<string>(0) == "AppCallback")
      .MustHaveHappenedOnceExactly();
    Assert.NotNull(_requestMessage.Content);
  }

  [Fact]
  public async Task SendCallbackRequestAsync_WithCustomHeaders_AddsHeadersToRequest()
  {
    var service = GetAppCallbackService();

    await service.SendCallbackRequestAsync(GetApp(new() { { "X-Test-Header", "value" } }), GetUser(), TestContext.Current.CancellationToken);

    Assert.True(_requestMessage.Headers.Contains("X-Test-Header"));
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync)
        && call.GetArgument<string>(0) == "AppCallback")
      .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task SendCallbackRequestAsync_Signs_Body_With_App_Secret()
  {
    var service = GetAppCallbackService();

    // An app-defined header must not be able to replace the signature
    await service.SendCallbackRequestAsync(GetApp(new() { { ZenConstants.X_SIGNATURE, "sha256=spoofed" } }), GetUser(), TestContext.Current.CancellationToken);

    var timestamp = Assert.Single(_requestMessage.Headers.GetValues(ZenConstants.X_TIMESTAMP));
    var signature = Assert.Single(_requestMessage.Headers.GetValues(ZenConstants.X_SIGNATURE));
    var body = await _requestMessage.Content!.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), timestamp);
    Assert.StartsWith(HmacSignatureUtil.SignaturePrefix, signature);
    Assert.True(HmacSignatureUtil.IsValidSignature(AppSecretKey, timestamp, body, signature, Now, TimeSpan.FromMinutes(5)));
  }

  private AppCallbackService GetAppCallbackService() =>
    new(_httpClientHelper, _jwtTokenHandler, _httpContextAccessor, _roleSettingsOptions, _timeProvider);

  [Fact]
  public async Task SendCallbackRequestAsync_Sends_The_New_Users_Details_As_An_Unconfirmed_Create()
  {
    var service = GetAppCallbackService();

    await service.SendCallbackRequestAsync(GetApp(), GetUser(), TestContext.Current.CancellationToken);

    using var body = JsonDocument.Parse(await _requestMessage.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
    var userContent = body.RootElement.GetProperty("userContent");

    Assert.Equal("CREATE", body.RootElement.GetProperty("eventType").GetString());
    Assert.Equal("user-id", userContent.GetProperty("userIdentityId").GetString());
    Assert.Equal("Test", userContent.GetProperty("firstName").GetString());
    Assert.Equal("User", userContent.GetProperty("lastName").GetString());
    Assert.Equal("test.user", userContent.GetProperty("userName").GetString());
    Assert.Equal("0712345678", userContent.GetProperty("phoneNumber").GetString());
    Assert.Equal("test.user@example.com", userContent.GetProperty("userEmail").GetString());
    Assert.False(userContent.GetProperty("phoneNumberConfirmed").GetBoolean());
    Assert.False(userContent.GetProperty("emailConfirmed").GetBoolean());
  }

  [Fact]
  public async Task SendCallbackRequestAsync_With_ForwardServiceToken_Sends_A_Service_Token_Scoped_To_The_Apps_Users()
  {
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim("testapp-user", ZenConstants.SCOPE_ACTION_CREATE)).Returns(new Claim("scope", "testapp-user:create"));
    A.CallTo(() => _jwtTokenHandler.CreateToken("useridentity", "useridentity", "TestApp",
        A<HashSet<string>>.That.Contains(ZenConstants.INTERNAL_SERVICE_ROLE), A<HashSet<string>>.That.Contains("testapp-user:create"), A<HashSet<Claim>?>._))
      .Returns(("service-token", 60));

    var service = GetAppCallbackService();
    var app = GetApp();
    app.ForwardServiceToken = true;

    await service.SendCallbackRequestAsync(app, GetUser(), TestContext.Current.CancellationToken);

    Assert.Equal("Bearer", _requestMessage.Headers.Authorization?.Scheme);
    Assert.Equal("service-token", _requestMessage.Headers.Authorization?.Parameter);
  }

  [Fact]
  public async Task SendCallbackRequestAsync_Without_ForwardServiceToken_Sends_No_Token()
  {
    var service = GetAppCallbackService();

    await service.SendCallbackRequestAsync(GetApp(), GetUser(), TestContext.Current.CancellationToken);

    Assert.Null(_requestMessage.Headers.Authorization);
    A.CallTo(() => _jwtTokenHandler.CreateToken(A<string>._, A<string>._, A<string>._, A<HashSet<string>>._, A<HashSet<string>>._, A<HashSet<Claim>?>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task SendCallbackRequestAsync_Outside_A_Request_Sends_A_New_Request_Id()
  {
    _httpContextAccessor.HttpContext = null;
    var service = GetAppCallbackService();

    await service.SendCallbackRequestAsync(GetApp(), GetUser(), TestContext.Current.CancellationToken);

    Assert.False(string.IsNullOrWhiteSpace(Assert.Single(_requestMessage.Headers.GetValues(ZenConstants.X_REQUEST_ID))));
  }

  private static RegisteredAppEntity GetApp(Dictionary<string, string>? callbackHeaders = null) => new()
  {
    AppName = "TestApp",
    AppSecretKey = AppSecretKey,
    CallbackUrl = "https://callback.test/cb",
    CallbackHeaders = callbackHeaders
  };

  private static UserRegisteredEvent GetUser() => new()
  {
    AppName = "TestApp",
    UserId = "user-id",
    FirstName = "Test",
    LastName = "User",
    UserName = "test.user",
    PhoneNumber = "0712345678",
    UserEmail = "test.user@example.com"
  };
}
