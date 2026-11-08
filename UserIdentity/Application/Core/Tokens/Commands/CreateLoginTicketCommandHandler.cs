using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record CreateLoginTicketCommand(string UserId, int AppId) : IBaseCommand;

public class CreateLoginTicketCommandHandler(
    IOptions<LoginTicketSettings> loginTicketSettingsOptions,
    ILoginTicketRepository loginTicketRepository,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>
{
    private readonly LoginTicketSettings _loginTicketSettings = loginTicketSettingsOptions.Value;
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

        var loginTicketEntity = new LoginTicketEntity
        {
            Ticket = _tokenFactory.HashToken(ticket),
            UserId = command.UserId,
            AppId = command.AppId,
            Expires = now.AddSeconds(_loginTicketSettings.ValidForSeconds),
        };

        loginTicketEntity.SetEntityAuditFields(command.UserId, now);

        _loginTicketRepository.CreateEntityItem(loginTicketEntity);

        return Task.FromResult(new LoginTicketViewModel { Ticket = ticket });
    }
}
