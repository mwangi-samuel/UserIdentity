using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record CreateLoginTicketCommand : IBaseCommand
{
    public Guid? Id { get; init; }

    public required string UserId { get; init; }

    public required int AppId { get; init; }

    public required SignInStep Step { get; init; }

    public string? CodeAction { get; init; }

    public string? CodeCorrelationId { get; init; }

    public bool EmailProven { get; init; }

    public bool ChangingMethod { get; init; }
}

public class CreateLoginTicketCommandHandler(
    IOptions<LoginTicketSettings> loginTicketSettingsOptions,
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    ILoginTicketRepository loginTicketRepository,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>
{
    private readonly LoginTicketSettings _loginTicketSettings = loginTicketSettingsOptions.Value;
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    public Task<LoginTicketViewModel> CreateItemAsync(
        CreateLoginTicketCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = _tokenFactory.GenerateToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var validForSeconds =
            command.CodeAction == UserMessages.WelcomeUserAction
                ? _loginTicketSettings.ValidForSeconds
                : _twoFactorSettings.TicketLifetimeSeconds;

        var loginTicketEntity = new LoginTicketEntity
        {
            Id = command.Id ?? Guid.CreateVersion7(),
            Ticket = _tokenFactory.HashToken(ticket),
            UserId = command.UserId,
            AppId = command.AppId,
            Expires = now.AddSeconds(validForSeconds),
            Step = command.Step,
            CodeAction = command.CodeAction,
            CodeCorrelationId = command.CodeCorrelationId,
            EmailProven = command.EmailProven,
            ChangingMethod = command.ChangingMethod,
        };

        loginTicketEntity.SetEntityAuditFields(command.UserId, now);

        _loginTicketRepository.CreateEntityItem(loginTicketEntity);

        return Task.FromResult(new LoginTicketViewModel { Ticket = ticket, Expires = loginTicketEntity.Expires });
    }
}
