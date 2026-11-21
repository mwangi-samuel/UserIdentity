using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record ChangeTwoFactorCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Password { get; init; } = null!;
}

public class ChangeTwoFactorCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler
) : ICreateItemCommandHandler<ChangeTwoFactorCommand, AuthUserViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        ChangeTwoFactorCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await _userManager.FindByIdAsync(command.UserId) ?? throw NoRecordException.ForIdentifier(command.UserId);

        await PasswordCheck.EnsureAsync(_userManager, _recordFailedSignInCommandHandler, user, command.Password, cancellationToken);

        var vm = await _signInUserCommandHandler.CreateItemAsync(
            new SignInUserCommand(user, command.RegisteredApp, command.RequestContext) { ChangingMethod = true },
            user.Id,
            cancellationToken
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vm;
    }
}
