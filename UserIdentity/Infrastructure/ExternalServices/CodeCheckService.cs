using System.Net;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.ExternalServices;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;

namespace UserIdentity.Infrastructure.ExternalServices;

public class CodeCheckService(
    IHttpRequestSender httpRequestSender,
    IJwtTokenHandler jwtTokenHandler,
    IRequestIdAccessor requestIdAccessor,
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<CodeCheckSettings> codeCheckSettingsOptions
) : BaseExternalService(jwtTokenHandler, requestIdAccessor, roleSettingsOptions), ICodeCheckService
{
    private readonly IHttpRequestSender _httpRequestSender = httpRequestSender;
    private readonly CodeCheckSettings _codeCheckSettings = codeCheckSettingsOptions.Value;

    public const string TargetServiceName = "CodeCheck";

    public async Task<CodeCheckOutcome> CheckAsync(
        string appName,
        string action,
        string correlationId,
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var response = await SendAsync(
            _codeCheckSettings.VerifyUrl,
            appName,
            new CodeCheckRequest(action, correlationId, code),
            cancellationToken
        );

        return ReadOutcome<CodeCheckOutcome>(response?.Outcome);
    }

    public async Task<CodeResendOutcome> ResendAsync(
        string appName,
        string action,
        string correlationId,
        CancellationToken cancellationToken = default
    )
    {
        var response = await SendAsync(
            _codeCheckSettings.ResendUrl,
            appName,
            new CodeResendRequest(action, correlationId),
            cancellationToken
        );

        return ReadOutcome<CodeResendOutcome>(response?.Outcome);
    }

    private async Task<CodeCheckResponse?> SendAsync(string url, string appName, object content, CancellationToken cancellationToken)
    {
        var requestMessage = _httpRequestSender
            .CreateHttpRequestMessage(HttpMethod.Post, url)
            .WithXRequestId(ResolveRequestId())
            .WithHttpContent(content)
            .WithBearerToken(
                GenerateServiceToken(
                    CurrentServiceName,
                    appName,
                    ResolveServiceResource(_codeCheckSettings.ServiceName, "message"),
                    ScopeActions.Create
                )
            );

        return await _httpRequestSender.SendRequestAsync<CodeCheckResponse>(
            TargetServiceName,
            requestMessage,
            HttpStatusCode.OK,
            cancellationToken
        );
    }

    private static TOutcome ReadOutcome<TOutcome>(string? outcome)
        where TOutcome : struct, Enum =>
        outcome is not null && Enum.GetNames<TOutcome>().Contains(outcome, StringComparer.Ordinal)
            ? Enum.Parse<TOutcome>(outcome)
            : throw new HttpRequestException($"The code service answered an unknown outcome: {outcome}");

    internal sealed record CodeCheckRequest(string Action, string CorrelationId, string Code);

    internal sealed record CodeResendRequest(string Action, string CorrelationId);

    internal sealed record CodeCheckResponse(string? Outcome);
}
