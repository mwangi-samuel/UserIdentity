using UserIdentity.Common.Enums;

namespace UserIdentity.Application.Interfaces;

public interface ICodeCheckService
{
    Task<CodeCheckOutcome> CheckAsync(
        string appName,
        string action,
        string correlationId,
        string code,
        CancellationToken cancellationToken = default
    );

    Task<CodeResendOutcome> ResendAsync(string appName, string action, string correlationId, CancellationToken cancellationToken = default);
}
