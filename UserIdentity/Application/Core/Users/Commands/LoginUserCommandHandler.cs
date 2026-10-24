using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entity;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Settings;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Users.Commands;

public record LoginUserCommand : IBaseCommand
{

  [JsonIgnore]
  [ValidateNever]
  public RegisteredAppEntity? RegisteredApp { get; internal set; }

  public int AppId { get; set; }

  [Required]
  public string UserName { get; init; } = null!;

  [Required]
  public string Password { get; init; } = null!;
}

public class LoginUserCommandHandler(
    IOptions<IdentityOptions> identityOptions,
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IRefreshTokenRepository refreshTokenRepository,
    IMachineDateTime machineDateTime,
    IGetItemQueryHandler<GetUserQuery, UserViewModel> getUserQueryHandler,
    IUserRegisteredAppRepository userRegisteredAppRepository
    ) : ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>
{
  private readonly IdentityOptions _identityOptions = identityOptions.Value;
  private readonly RefreshTokenSettings _refreshTokenSettings = refreshTokenSettingsOptions.Value;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory = tokenFactory;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IMachineDateTime _machineDateTime = machineDateTime;
  private readonly IGetItemQueryHandler<GetUserQuery, UserViewModel> _getUserQueryHandler = getUserQueryHandler;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;

  public async Task<AuthUserViewModel> CreateItemAsync(LoginUserCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var user = (
            await _userManager.FindByNameAsync(command.UserName)
            ?? await _userManager.FindByEmailAsync(command.UserName)
            ) ?? throw new InvalidCredentialException("No Identity User for User Name - " + command.UserName);

    if (await _userManager.IsLockedOutAsync(user))
      throw new InvalidCredentialException("Locked Out Identity User - " + command.UserName);

    var userExists = await _userManager.CheckPasswordAsync(user, command.Password);

    if (!userExists)
    {
      await _userManager.AccessFailedAsync(user);
      throw new InvalidCredentialException("Invalid Credentials Provided for - " + command.UserName);
    }

    await _userManager.ResetAccessFailedCountAsync(user);

    UserDto userDto;

    try
    {
      userDto = (await _getUserQueryHandler.GetItemAsync(new GetUserQuery { UserId = user.Id, AppName = command.RegisteredApp!.AppName }, cancellationToken)).User;
    }
    catch (NoRecordException ex)
    {
      throw new InvalidCredentialException("No User Record for Identity User - " + command.UserName, ex);
    }

    // We only include the accessToken in the payload, when user is confirmed.
    var isConfirmed = !_identityOptions.SignIn.RequireConfirmedAccount || user.EmailConfirmed || user.PhoneNumberConfirmed;
    AccessTokenViewModel? userToken = null;

    if (isConfirmed)
    {
      var refreshToken = _tokenFactory.GenerateToken();

      (var token, var expiresIn) = _jwtTokenHandler.CreateToken(user.Id, user.UserName!, command.RegisteredApp!.AppName, userDto.Roles, userDto.RoleClaims);

      var newRefreshToken = new RefreshTokenEntity
      {
        Expires = _machineDateTime.Now.AddSeconds(_refreshTokenSettings.ValidForSeconds),
        UserId = user.Id,
        Token = _tokenFactory.HashToken(refreshToken),
      };

      newRefreshToken.SetEntityAuditFields(user.Id, _machineDateTime.Now);

      await _refreshTokenRepository.RemoveExpiredEntityItemsAsync(user.Id, _machineDateTime.Now, cancellationToken);

      _refreshTokenRepository.CreateEntityItem(newRefreshToken);

      await _unitOfWork.SaveChangesAsync(cancellationToken);

      userToken = new AccessTokenViewModel
      {
        AccessToken = new AccessTokenDto { Token = token, ExpiresIn = expiresIn },
        RefreshToken = refreshToken,
      };
    }

    return new AuthUserViewModel
    {
      User = userDto,
      UserToken = userToken,
      IsConfirmed = isConfirmed,
    };
  }
}
