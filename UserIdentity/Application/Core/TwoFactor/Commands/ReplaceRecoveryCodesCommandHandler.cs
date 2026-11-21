using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record ReplaceRecoveryCodesCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [Required]
    public string Password { get; init; } = null!;
}

public class ReplaceRecoveryCodesCommandHandler(
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    UserManager<IdentityUser> userManager,
    IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> getTwoFactorStatusQueryHandler,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler
) : ICreateItemCommandHandler<ReplaceRecoveryCodesCommand, RecoveryCodesViewModel>
{
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> _getTwoFactorStatusQueryHandler =
        getTwoFactorStatusQueryHandler;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;

    public async Task<RecoveryCodesViewModel> CreateItemAsync(
        ReplaceRecoveryCodesCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await _userManager.FindByIdAsync(command.UserId) ?? throw NoRecordException.ForIdentifier(command.UserId);

        await PasswordCheck.EnsureAsync(_userManager, _recordFailedSignInCommandHandler, user, command.Password, cancellationToken);

        var status = await _getTwoFactorStatusQueryHandler.GetItemAsync(
            new GetTwoFactorStatusQuery(user.Id, command.RegisteredApp),
            cancellationToken
        );

        if (status.Method is null)
            throw new IllegalOperationException(ErrorMessages.TwoFactor.NotOn);

        var recoveryCodes =
            await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, _twoFactorSettings.RecoveryCodeCount)
            ?? throw new InvalidOperationException($"No recovery codes were made for user {user.Id}");

        return new RecoveryCodesViewModel { RecoveryCodes = [.. recoveryCodes] };
    }
}
