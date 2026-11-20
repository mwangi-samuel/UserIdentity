using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.PasswordResets;

namespace UserIdentity.Application.Core.Users.Commands;

public record ResendCodeCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    public string? Ticket { get; init; }

    public string? ResetRequestId { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Ticket) == string.IsNullOrWhiteSpace(ResetRequestId))
            yield return new ValidationResult(ErrorMessages.Users.TicketOrResetRequestIdRequired, [nameof(Ticket), nameof(ResetRequestId)]);
    }
}

public class ResendCodeCommandHandler(
    ITokenFactory tokenFactory,
    ILoginTicketRepository loginTicketRepository,
    IPasswordResetRepository passwordResetRepository,
    ICodeCheckService codeCheckService,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ResendCodeCommand, ResendCodeViewModel>
{
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly IPasswordResetRepository _passwordResetRepository = passwordResetRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<ResendCodeViewModel> CreateItemAsync(
        ResendCodeCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var isTicket = !string.IsNullOrWhiteSpace(command.Ticket);

        var sentCode = isTicket
            ? await FindTicketCodeAsync(command, cancellationToken)
            : await FindPasswordResetCodeAsync(command, cancellationToken);

        (var action, var correlationId) =
            sentCode ?? (isTicket ? UserMessages.WelcomeUserAction : UserMessages.PasswordResetAction, Guid.CreateVersion7().ToString());

        await _codeCheckService.ResendAsync(command.RegisteredApp.AppName, action, correlationId, cancellationToken);

        return new ResendCodeViewModel();
    }

    private async Task<(string Action, string CorrelationId)?> FindTicketCodeAsync(
        ResendCodeCommand command,
        CancellationToken cancellationToken
    )
    {
        var loginTicket = await _loginTicketRepository.GetEntityByAlternateIdAsync(
            new LoginTicketEntity { Ticket = _tokenFactory.HashToken(command.Ticket!), AppId = command.RegisteredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        return
            loginTicket is { CodeAction: { } action, CodeCorrelationId: { } correlationId }
            && loginTicket.Expires > _timeProvider.GetUtcNow().UtcDateTime
            ? (action, correlationId)
            : null;
    }

    private async Task<(string Action, string CorrelationId)?> FindPasswordResetCodeAsync(
        ResendCodeCommand command,
        CancellationToken cancellationToken
    )
    {
        var passwordReset = await _passwordResetRepository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = _tokenFactory.HashToken(command.ResetRequestId!), AppId = command.RegisteredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        return passwordReset is null || passwordReset.Applied || passwordReset.Expires <= _timeProvider.GetUtcNow().UtcDateTime
            ? null
            : (UserMessages.PasswordResetAction, passwordReset.Id.ToString());
    }
}
