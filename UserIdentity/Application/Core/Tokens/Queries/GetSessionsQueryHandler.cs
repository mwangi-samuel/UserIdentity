using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Tokens.Queries;

public record GetSessionsQuery : IBaseQuery
{
    public required string UserId { get; init; }

    public required RegisteredAppDto RegisteredApp { get; init; }

    public required UserChanger Changer { get; init; }

    public Guid? CurrentSessionId { get; init; }
}

public class GetSessionsQueryHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IRefreshTokenRepository refreshTokenRepository,
    TimeProvider timeProvider
) : IGetItemsQueryHandler<GetSessionsQuery, SessionsViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SessionsViewModel> GetItemsAsync(GetSessionsQuery query, CancellationToken cancellationToken = default)
    {
        var user = await UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            await _userManager.FindByIdAsync(query.UserId),
            query.UserId,
            query.RegisteredApp.AppName,
            query.Changer,
            cancellationToken
        );

        var sessions = await _refreshTokenRepository.GetEntityItemsAsync(
            user.Id,
            query.RegisteredApp.Id,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken
        );

        return new SessionsViewModel
        {
            Sessions =
            [
                .. sessions.Select(session => new SessionDto
                {
                    Id = session.Id,
                    SignedInAt = session.CreatedAt,
                    LastActiveAt = session.UpdatedAt ?? session.CreatedAt,
                    ExpiresAt = session.Expires,
                    IpAddress = session.IpAddress,
                    Device = session.Device,
                    Current = session.Id == query.CurrentSessionId,
                }),
            ],
        };
    }
}
