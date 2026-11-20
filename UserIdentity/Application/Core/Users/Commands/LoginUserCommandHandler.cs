using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;

namespace UserIdentity.Application.Core.Users.Commands;

public record LoginUserCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string UserName { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}

public class LoginUserCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler
) : ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>
{
    private static readonly string _dummyPasswordHash = new PasswordHasher<IdentityUser>().HashPassword(
        new IdentityUser(),
        Guid.NewGuid().ToString()
    );

    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        LoginUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await _userManager.FindByNameAsync(command.UserName) ?? await _userManager.FindByEmailAsync(command.UserName);

        if (user is null || await _userManager.IsLockedOutAsync(user))
        {
            _userManager.PasswordHasher.VerifyHashedPassword(user ?? new IdentityUser(), _dummyPasswordHash, command.Password);

            throw new InvalidCredentialException(user is null ? "Unknown user name or email" : $"User {user.Id} is locked out");
        }

        if (!await _userManager.CheckPasswordAsync(user, command.Password))
        {
            await _recordFailedSignInCommandHandler.CreateItemAsync(new RecordFailedSignInCommand(user, false), user.Id, cancellationToken);

            throw new InvalidCredentialException($"Wrong password for user {user.Id}");
        }

        var vm = await _signInUserCommandHandler.CreateItemAsync(
            new SignInUserCommand(user, command.RegisteredApp, command.RequestContext),
            user.Id,
            cancellationToken
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vm;
    }
}
