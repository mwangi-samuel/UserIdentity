using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Interfaces;

namespace UserIdentity.Infrastructure.ExternalServices;

public class AppCallbackService(
    IHttpRequestSender httpRequestSender,
    IJwtTokenHandler jwtTokenHandler,
    IRequestIdAccessor requestIdAccessor,
    IOptions<RoleSettings> roleSettingsOptions,
    TimeProvider timeProvider
) : BaseExternalService(jwtTokenHandler, requestIdAccessor, roleSettingsOptions), IAppCallbackService
{
    private readonly IHttpRequestSender _httpRequestSender = httpRequestSender;
    private readonly TimeProvider _timeProvider = timeProvider;

    private const string TargetServiceName = "AppCallback";

    public async Task SendCallbackRequestAsync(
        ActiveRegisteredApp registeredApp,
        UserRegisteredEvent user,
        CancellationToken cancellationToken = default
    )
    {
        var content = new
        {
            UserContent = new
            {
                UserIdentityId = user.UserId,
                user.FirstName,
                user.LastName,
                user.UserName,
                user.PhoneNumber,
                PhoneNumberConfirmed = false,
                user.UserEmail,
                EmailConfirmed = false,
            },
            EventType = CrudEvent.Create,
        };

        var requestMessage = _httpRequestSender
            .CreateHttpRequestMessage(HttpMethod.Post, registeredApp.CallbackUrl!)
            .WithXRequestId(ResolveRequestId())
            .WithHttpContent(content);

        if (registeredApp.ForwardServiceToken)
            requestMessage = requestMessage.WithBearerToken(
                GenerateServiceToken(
                    CurrentServiceName,
                    registeredApp.AppName,
                    ResolveServiceResource(registeredApp.AppName, "user"),
                    ScopeActions.Create
                )
            );

        if (registeredApp.CallbackHeaders != null && registeredApp.CallbackHeaders.Count > 0)
            foreach (var header in registeredApp.CallbackHeaders)
                requestMessage = requestMessage.WithRequestHeader(header.Key, header.Value);

        // Signed last so app-defined callback headers cannot replace X-Timestamp / X-Signature.
        // The app verifies the body with its secret (see HmacSignature.IsValid).
        await requestMessage.WithHmacSignatureAsync(registeredApp.AppSecretKey, _timeProvider, cancellationToken);

        await _httpRequestSender.SendRequestAsync(TargetServiceName, requestMessage, cancellationToken);
    }
}
