using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record StartSessionCommand(string UserId, string UserName, string AppName) : IBaseCommand;

public class StartSessionCommandHandler(
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> createAccessTokenCommandHandler,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>
{
    private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettingsOptions.Value;
    private readonly ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> _createAccessTokenCommandHandler =
        createAccessTokenCommandHandler;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<UserTokenViewModel> CreateItemAsync(
        StartSessionCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var accessToken = await _createAccessTokenCommandHandler.CreateItemAsync(
            new CreateAccessTokenCommand(command.UserId, command.UserName, command.AppName),
            userId,
            cancellationToken
        );
        var refreshToken = _tokenFactory.GenerateToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var refreshTokenEntity = new RefreshTokenEntity
        {
            UserId = command.UserId,
            Token = _tokenFactory.HashToken(refreshToken),
            Expires = now.AddSeconds(_refreshTokenSettings.ValidForSeconds),
            Version = Guid.NewGuid(),
        };

        refreshTokenEntity.SetEntityAuditFields(command.UserId, now);

        _refreshTokenRepository.CreateEntityItem(refreshTokenEntity);

        return new UserTokenViewModel
        {
            UserToken = new UserTokenDto { AccessToken = accessToken.AccessToken, RefreshToken = refreshToken },
        };
    }
}
