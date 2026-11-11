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
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

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
    ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> startSessionCommandHandler,
    ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> createLoginTicketCommandHandler
) : ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>
{
    private static readonly string _dummyPasswordHash = new PasswordHasher<IdentityUser>().HashPassword(
        new IdentityUser(),
        Guid.NewGuid().ToString()
    );

    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler =
        createLoginTicketCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        LoginUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var appName = command.RegisteredApp.AppName;

        var user = await _userManager.FindByNameAsync(command.UserName) ?? await _userManager.FindByEmailAsync(command.UserName);

        if (user is null || await _userManager.IsLockedOutAsync(user))
        {
            _userManager.PasswordHasher.VerifyHashedPassword(user ?? new IdentityUser(), _dummyPasswordHash, command.Password);

            throw new InvalidCredentialException(user is null ? "Unknown user name or email" : $"User {user.Id} is locked out");
        }

        if (!await _userManager.CheckPasswordAsync(user, command.Password))
        {
            await RecordFailedAccessAsync(user);
            throw new InvalidCredentialException($"Wrong password for user {user.Id}");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var userDto =
            await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
            ?? throw new InvalidCredentialException($"User {user.Id} has no profile for app {appName}");

        var isConfirmed = AccountConfirmation.IsConfirmed(_identityOptions, user);
        UserTokenViewModel? session = null;
        LoginTicketViewModel? loginTicket = null;

        if (isConfirmed)
            session = await _startSessionCommandHandler.CreateItemAsync(
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
            UserToken = session?.UserToken,
            IsConfirmed = isConfirmed,
            LoginTicket = loginTicket?.Ticket,
        };
    }

    private async Task RecordFailedAccessAsync(IdentityUser user)
    {
        var result = await _userManager.AccessFailedAsync(user);

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            _userRepository.ClearTrackedEntities();

            var reloadedUser = await _userManager.FindByIdAsync(user.Id);

            if (reloadedUser is null)
                return;

            result = await _userManager.AccessFailedAsync(reloadedUser);
        }

        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
