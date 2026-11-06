using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;

namespace UserIdentity.Application.Core.Tokens.Shared;

public class UserTokenIssuer(
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider,
    RoleSettings roleSettings,
    RefreshTokenSettings refreshTokenSettings
)
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly RoleSettings _roleSettings = roleSettings;
    private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettings;

    public async Task<AccessTokenDto> CreateAccessTokenAsync(
        string userId,
        string userName,
        string appName,
        CancellationToken cancellationToken = default
    )
    {
        var roles = AppRoleNames.ForApp(
            await _roleRepository.GetUserRoleNamesAsync(userId, cancellationToken),
            appName,
            _roleSettings.GlobalAdminRole
        );
        var scopes = await _roleRepository.GetScopesAsync(roles, cancellationToken);

        (var token, var expiresIn) = _jwtTokenHandler.CreateToken(userId, userName, appName, [.. roles], scopes);

        return new AccessTokenDto { Token = token, ExpiresIn = expiresIn };
    }

    // Adds the refresh token to the repository; the caller saves it
    public async Task<AccessTokenViewModel> StartSessionAsync(
        string userId,
        string userName,
        string appName,
        CancellationToken cancellationToken = default
    )
    {
        var accessToken = await CreateAccessTokenAsync(userId, userName, appName, cancellationToken);
        var refreshToken = _tokenFactory.GenerateToken();

        var refreshTokenEntity = new RefreshTokenEntity
        {
            UserId = userId,
            Token = _tokenFactory.HashToken(refreshToken),
            Expires = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(_refreshTokenSettings.ValidForSeconds),
            Version = Guid.NewGuid(),
        };

        refreshTokenEntity.SetEntityAuditFields(userId, _timeProvider.GetUtcNow().UtcDateTime);

        _refreshTokenRepository.CreateEntityItem(refreshTokenEntity);

        return new AccessTokenViewModel { AccessToken = accessToken, RefreshToken = refreshToken };
    }
}
