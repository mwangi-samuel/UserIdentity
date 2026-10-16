using Microsoft.Extensions.Options;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Interfaces;

namespace UserIdentity.Infrastructure.ExternalServices;

public class AppCallbackService(
    IHttpClientHelper httpClientHelper,
    IJwtTokenHandler jwtTokenHandler,
    IHttpContextAccessor httpContextAccessor,
    IOptions<RoleSettings> roleSettingsOptions,
    TimeProvider timeProvider
    ) : BaseExternalService(jwtTokenHandler, httpContextAccessor, roleSettingsOptions),
            IAppCallbackService
{
  private readonly IHttpClientHelper _httpClientHelper = httpClientHelper;
  private readonly TimeProvider _timeProvider = timeProvider;

  private const string targetServiceName = "AppCallback";

  public async Task SendCallbackRequestAsync(UserUpdateEvent userUpdateEvent, CancellationToken cancellationToken = default)
  {
    var externalServiceName = userUpdateEvent.RegisteredApp.AppName!;
    var externalServiceUrl = userUpdateEvent.RegisteredApp.CallbackUrl!;

    var xRequestId = ResolveRequestId();

    var content = new
    {
      userUpdateEvent.UserContent,
      userUpdateEvent.EventType
    };

    var requestMessage = _httpClientHelper.CreateHttpRequestMessage(HttpMethod.Post, externalServiceUrl)
        .WithXRequestId(xRequestId)
        .WithHttpContent(content);

    if (userUpdateEvent.RegisteredApp.ForwardServiceToken)
      requestMessage = requestMessage.WithBearerToken(
          GenerateServiceToken(
              CurrentServiceName,
              userUpdateEvent.RegisteredApp.AppName,
              ResolveServiceResource(externalServiceName, "user"),
              ZenConstants.SCOPE_ACTION_CREATE
              )
          );


    if (userUpdateEvent.RegisteredApp.CallbackHeaders != null && userUpdateEvent.RegisteredApp.CallbackHeaders.Count > 0)
      foreach (var header in userUpdateEvent.RegisteredApp.CallbackHeaders)
        requestMessage = requestMessage.WithRequestHeader(header.Key, header.Value);

    // Signed last so app-defined callback headers cannot replace X-Timestamp / X-Signature.
    // The app verifies the body with its secret (see HmacSignatureUtil.IsValidSignature).
    await requestMessage.WithHmacSignatureAsync(userUpdateEvent.RegisteredApp.AppSecretKey, _timeProvider, cancellationToken);

    await _httpClientHelper.SendRequestAsync(targetServiceName, requestMessage, cancellationToken);
  }
}
