using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.ExternalServices;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Enums;
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

    await service.SendCallbackRequestAsync(GetUserUpdateEvent());

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

    await service.SendCallbackRequestAsync(GetUserUpdateEvent(new() { { "X-Test-Header", "value" } }));

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
    await service.SendCallbackRequestAsync(GetUserUpdateEvent(new() { { ZenConstants.X_SIGNATURE, "sha256=spoofed" } }));

    var timestamp = Assert.Single(_requestMessage.Headers.GetValues(ZenConstants.X_TIMESTAMP));
    var signature = Assert.Single(_requestMessage.Headers.GetValues(ZenConstants.X_SIGNATURE));
    var body = await _requestMessage.Content!.ReadAsByteArrayAsync();

    Assert.Equal(Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), timestamp);
    Assert.StartsWith(HmacSignatureUtil.SignaturePrefix, signature);
    Assert.True(HmacSignatureUtil.IsValidSignature(AppSecretKey, timestamp, body, signature, Now, TimeSpan.FromMinutes(5)));
    Assert.False(HmacSignatureUtil.IsValidSignature("some-other-secret", timestamp, body, signature, Now, TimeSpan.FromMinutes(5)));
  }

  private AppCallbackService GetAppCallbackService() =>
    new(_httpClientHelper, _jwtTokenHandler, _httpContextAccessor, _roleSettingsOptions, _timeProvider);

  private static UserUpdateEvent GetUserUpdateEvent(Dictionary<string, string>? callbackHeaders = null) => new()
  {
    RegisteredApp = new RegisteredAppEntity
    {
      AppName = "TestApp",
      AppSecretKey = AppSecretKey,
      CallbackUrl = "https://callback.test/cb",
      CallbackHeaders = callbackHeaders
    },
    RequestSource = RequestSource.UI,
    EventType = CrudEvent.CREATE,
    UserContent = new UserEventContent { UserIdentityId = "user-id" }
  };
}
