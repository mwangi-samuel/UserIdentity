using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record ExchangeRefreshTokenCommand : IBaseCommand
{
    [Required]
    public string AccessToken { get; init; } = null!;

    [Required]
    public string RefreshToken { get; init; } = null!;
}

public class ExchangeRefreshTokenCommandHandler(
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IRefreshTokenRepository refreshTokenRepository,
    TimeProvider timeProvider,
    ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> createAccessTokenCommandHandler
)
    : BaseAuditableItemCommandHandler<Guid, RefreshTokenEntity, IRefreshTokenRepository>(unitOfWork, refreshTokenRepository, timeProvider),
        IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>
{
    private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettingsOptions.Value;
    private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
    private readonly ITokenFactory _tokenFactory = tokenFactory;

    private readonly ICreateItemCommandHandler<CreateAccessTokenCommand, AccessTokenViewModel> _createAccessTokenCommandHandler =
        createAccessTokenCommandHandler;

    public async Task<ExchangeRefreshTokenViewModel> UpdateItemAsync(
        ExchangeRefreshTokenCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        // The access token being exchanged is expected to have expired, so only its signature and claims are validated
        var tokenValidationResult = await _jwtTokenHandler.ValidateTokenAsync(command.AccessToken, validateLifetime: false);

        var tokenUserId = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub);
        var tokenUserName = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name);
        var azp = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp);

        if (tokenUserId == null || tokenUserName == null || azp == null)
            throw new SecurityTokenException("Invalid access token provided");

        var requestRefreshTokenHash = _tokenFactory.HashToken(command.RefreshToken);

        var existing = await Repository.GetEntityForAppAsync(tokenUserId, requestRefreshTokenHash, azp, cancellationToken);

        if (existing is null)
        {
            // A replaced refresh token is being reused - end its session
            var reusedEntity = await Repository.GetEntityByPreviousTokenAsync(tokenUserId, requestRefreshTokenHash, cancellationToken);

            if (reusedEntity is not null)
                await RemoveRefreshTokenAsync(reusedEntity, cancellationToken);

            throw new SecurityTokenException("Invalid refresh token provided");
        }

        (var existingEntity, var isUserRegisteredForApp) = existing.Value;

        if (existingEntity.Expires <= TimeProvider.GetUtcNow().UtcDateTime)
        {
            await RemoveRefreshTokenAsync(existingEntity, cancellationToken);

            throw new SecurityTokenException("Expired refresh token provided");
        }

        // Ends sessions from logins to apps the user isn't registered for
        if (!isUserRegisteredForApp)
        {
            await RemoveRefreshTokenAsync(existingEntity, cancellationToken);

            throw new SecurityTokenException("Invalid refresh token provided");
        }

        var accessToken = await _createAccessTokenCommandHandler.CreateItemAsync(
            new CreateAccessTokenCommand(tokenUserId, tokenUserName, azp),
            tokenUserId,
            cancellationToken
        );

        var updateRefreshToken = _tokenFactory.GenerateToken();

        existingEntity.PreviousToken = existingEntity.Token;
        existingEntity.Token = _tokenFactory.HashToken(updateRefreshToken);
        existingEntity.Expires = TimeProvider.GetUtcNow().UtcDateTime.AddSeconds(_refreshTokenSettings.ValidForSeconds);
        existingEntity.Version = Guid.NewGuid();

        await UpdateEntityItemAsync(existingEntity, tokenUserId, cancellationToken);

        return new ExchangeRefreshTokenViewModel
        {
            UserToken = new UserTokenDto { AccessToken = accessToken.AccessToken, RefreshToken = updateRefreshToken },
        };
    }

    private async Task RemoveRefreshTokenAsync(RefreshTokenEntity refreshTokenEntity, CancellationToken cancellationToken)
    {
        Repository.RemoveEntityItem(refreshTokenEntity);

        await UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
