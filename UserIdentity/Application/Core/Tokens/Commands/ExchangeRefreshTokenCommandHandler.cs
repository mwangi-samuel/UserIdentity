using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entity;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Tokens.Settings;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

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
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IRefreshTokenRepository refreshTokenRepository,
    IMachineDateTime machineDateTime,
    IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> getRoleClaimsQueryHandler
    ) : IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>
{
  private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettingsOptions.Value;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory = tokenFactory;

  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler = getRoleClaimsQueryHandler;

  public async Task<ExchangeRefreshTokenViewModel> UpdateItemAsync(ExchangeRefreshTokenCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var requestAccessToken = ObjectUtil.RequireNonNullValue(command.AccessToken, nameof(command.AccessToken));
    var requestRefreshToken = ObjectUtil.RequireNonNullValue(command.RefreshToken, nameof(command.RefreshToken));

    // The access token being exchanged is expected to have expired, so only its signature and claims are validated
    var tokenValidationResult = await _jwtTokenHandler.ValidateTokenAsync(requestAccessToken, validateLifetime: false);

    var tokenUserId = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Sub);
    var tokenUserName = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Name);
    var azp = _jwtTokenHandler.ResolveTokenValue<string?>(tokenValidationResult, JwtRegisteredClaimNames.Azp);

    if (tokenUserId == null || tokenUserName == null || azp == null)
      throw new SecurityTokenException("Invalid access token provided");

    var requestRefreshTokenHash = _tokenFactory.HashToken(requestRefreshToken);

    var tokenEntity = new RefreshTokenEntity
    {
      UserId = tokenUserId,
      Token = requestRefreshTokenHash
    };

    var existingEntity = await _refreshTokenRepository.GetEntityByAlternateIdAsync(tokenEntity, QueryCondition.MAY_OR_MAY_NOT_EXIST, cancellationToken);

    if (existingEntity is null)
    {
      // A replaced refresh token is being reused - end its session
      var reusedEntity = await _refreshTokenRepository.GetEntityByPreviousTokenAsync(tokenUserId, requestRefreshTokenHash, cancellationToken);

      if (reusedEntity is not null)
        await RemoveRefreshTokenAsync(reusedEntity, cancellationToken);

      throw new SecurityTokenException("Invalid refresh token provided");
    }

    if (existingEntity.Expires <= _machineDateTime.Now)
    {
      await RemoveRefreshTokenAsync(existingEntity, cancellationToken);

      throw new SecurityTokenException("Expired refresh token provided");
    }

    var userRoles = await _userManager.GetRolesAsync(new IdentityUser { Id = tokenUserId });

    var userRoleClaims = await _getRoleClaimsQueryHandler.GetItemsAsync(new GetRoleClaimsForRolesQuery { Roles = userRoles }, cancellationToken);

    var updateRefreshToken = _tokenFactory.GenerateToken();

    (var token, var expiresIn) = _jwtTokenHandler.CreateToken(tokenUserId, tokenUserName, azp, [.. userRoles], userRoleClaims.RoleClaims);

    existingEntity.PreviousToken = existingEntity.Token;
    existingEntity.Token = _tokenFactory.HashToken(updateRefreshToken);
    existingEntity.Expires = _machineDateTime.Now.AddSeconds(_refreshTokenSettings.ValidForSeconds);
    existingEntity.UpdateEntityAuditFields(tokenUserId, _machineDateTime.Now);

    _refreshTokenRepository.UpdateEntityItem(existingEntity);

    await _unitOfWork.SaveChangesAsync(cancellationToken);

    return new ExchangeRefreshTokenViewModel
    {
      UserToken = new AccessTokenViewModel
      {
        AccessToken = new AccessTokenDTO { Token = token, ExpiresIn = expiresIn },
        RefreshToken = updateRefreshToken,
      },
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
