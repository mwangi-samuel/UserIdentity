using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record ExchangeRefreshTokenCommand : IBaseCommand
{
  [Required]
  public string? AccessToken { get; init; }

  [Required]
  public string? RefreshToken { get; init; }
}

public class ExchangeRefreshTokenCommandHandler(
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IRefreshTokenRepository refreshTokenRepository,
    IRoleRepository roleRepository,
    TimeProvider timeProvider,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IOptions<RoleSettings> roleSettingsOptions
    ) : IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>
{
  private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettingsOptions.Value;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory = tokenFactory;

  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly TimeProvider _timeProvider = timeProvider;

  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly UserTokenIssuer _userTokenIssuer = new(
    roleRepository,
    refreshTokenRepository,
    jwtTokenHandler,
    tokenFactory,
    timeProvider,
    roleSettingsOptions.Value,
    refreshTokenSettingsOptions.Value);

  public async Task<ExchangeRefreshTokenViewModel> UpdateItemAsync(
    ExchangeRefreshTokenCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    var requestAccessToken = ObjectExtensions.RequireNonNullValue(command.AccessToken, nameof(command.AccessToken));
    var requestRefreshToken = ObjectExtensions.RequireNonNullValue(command.RefreshToken, nameof(command.RefreshToken));

    // The access token being exchanged is expected to have expired, so only its signature and claims are validated
    var tokenValidationResult = await _jwtTokenHandler.ValidateTokenAsync(requestAccessToken, validateLifetime: false);

    var tokenUserId = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub);
    var tokenUserName = _jwtTokenHandler.ResolveTokenValue<string?>(
      tokenValidationResult,
      JwtRegisteredClaimNames.Name);
    var azp = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp);

    if (tokenUserId == null || tokenUserName == null || azp == null)
      throw new SecurityTokenException("Invalid access token provided");

    var requestRefreshTokenHash = _tokenFactory.HashToken(requestRefreshToken);

    var tokenEntity = new RefreshTokenEntity { UserId = tokenUserId, Token = requestRefreshTokenHash };

    var existingEntity = await _refreshTokenRepository.GetEntityByAlternateIdAsync(
      tokenEntity,
      QueryCondition.MayOrMayNotExist,
      cancellationToken);

    if (existingEntity is null)
    {
      // A replaced refresh token is being reused - end its session
      var reusedEntity = await _refreshTokenRepository.GetEntityByPreviousTokenAsync(
        tokenUserId,
        requestRefreshTokenHash,
        cancellationToken);

      if (reusedEntity is not null)
        await RemoveRefreshTokenAsync(reusedEntity, cancellationToken);

      throw new SecurityTokenException("Invalid refresh token provided");
    }

    if (existingEntity.Expires <= _timeProvider.GetUtcNow().UtcDateTime)
    {
      await RemoveRefreshTokenAsync(existingEntity, cancellationToken);

      throw new SecurityTokenException("Expired refresh token provided");
    }

    // Ends sessions from logins to apps the user isn't registered for
    if (!await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(tokenUserId, azp, cancellationToken))
    {
      await RemoveRefreshTokenAsync(existingEntity, cancellationToken);

      throw new SecurityTokenException("Invalid refresh token provided");
    }

    var accessToken = await _userTokenIssuer.CreateAccessTokenAsync(tokenUserId, tokenUserName, azp, cancellationToken);

    var updateRefreshToken = _tokenFactory.GenerateToken();

    existingEntity.PreviousToken = existingEntity.Token;
    existingEntity.Token = _tokenFactory.HashToken(updateRefreshToken);
    existingEntity.Expires = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(_refreshTokenSettings.ValidForSeconds);
    existingEntity.Version = Guid.NewGuid();
    existingEntity.UpdateEntityAuditFields(tokenUserId, _timeProvider.GetUtcNow().UtcDateTime);

    _refreshTokenRepository.UpdateEntityItem(existingEntity);

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return new ExchangeRefreshTokenViewModel
    {
      UserToken = new AccessTokenViewModel { AccessToken = accessToken, RefreshToken = updateRefreshToken },
      EditEnabled = false,
      DeleteEnabled = false
    };

  }

  private async Task RemoveRefreshTokenAsync(RefreshTokenEntity refreshTokenEntity, CancellationToken cancellationToken)
  {
    _refreshTokenRepository.RemoveEntityItem(refreshTokenEntity);

    await _unitOfWork.SaveChangesAsync(cancellationToken);
  }
}
