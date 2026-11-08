using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record LoginUserCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp? RegisteredApp { get; internal set; }

    public int AppId { get; set; }

    [Required]
    public string UserName { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}

public class LoginUserCommandHandler(
    IOptions<IdentityOptions> identityOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ICreateItemCommandHandler<StartSessionCommand, AccessTokenViewModel> startSessionCommandHandler,
    ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> createLoginTicketCommandHandler
) : ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>
{
    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICreateItemCommandHandler<StartSessionCommand, AccessTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler =
        createLoginTicketCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        LoginUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var appName = command.RegisteredApp!.AppName;

        var user =
            (await _userManager.FindByNameAsync(command.UserName) ?? await _userManager.FindByEmailAsync(command.UserName))
            ?? throw new InvalidCredentialException("Unknown user name or email");

        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidCredentialException($"User {user.Id} is locked out");

        if (!await _userManager.CheckPasswordAsync(user, command.Password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new InvalidCredentialException($"Wrong password for user {user.Id}");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var userDto =
            await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
            ?? throw new InvalidCredentialException($"User {user.Id} has no profile for app {appName}");

        var isConfirmed = AccountConfirmation.IsConfirmed(_identityOptions, user);
        AccessTokenViewModel? userToken = null;
        LoginTicketViewModel? loginTicket = null;

        if (isConfirmed)
            userToken = await _startSessionCommandHandler.CreateItemAsync(
                new StartSessionCommand(user.Id, user.UserName!, appName),
                user.Id,
                cancellationToken
            );
        else
            loginTicket = await _createLoginTicketCommandHandler.CreateItemAsync(
                new CreateLoginTicketCommand(user.Id, command.RegisteredApp.Id),
                user.Id,
                cancellationToken
            );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthUserViewModel
        {
            User = userDto,
            UserToken = userToken,
            IsConfirmed = isConfirmed,
            LoginTicket = loginTicket?.Ticket,
        };
    }
}
