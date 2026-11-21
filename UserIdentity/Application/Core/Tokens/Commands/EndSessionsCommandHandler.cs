using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record EndSessionsCommand(string UserId, RegisteredAppDto RegisteredApp, UserChanger Changer, Guid? CurrentSessionId) : IBaseCommand;

public class EndSessionsCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IRefreshTokenRepository refreshTokenRepository
) : IDeleteItemCommandHandler<EndSessionsCommand, DeleteRecordViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        EndSessionsCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            await _userManager.FindByIdAsync(command.UserId),
            command.UserId,
            command.RegisteredApp.AppName,
            command.Changer,
            cancellationToken
        );

        await _refreshTokenRepository.DeleteEntityItemsAsync(
            user.Id,
            command.RegisteredApp.Id,
            exceptId: command.CurrentSessionId,
            cancellationToken: cancellationToken
        );

        return new DeleteRecordViewModel();
    }
}
