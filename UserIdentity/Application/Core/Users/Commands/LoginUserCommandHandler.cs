using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.Users;

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
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IMachineDateTime machineDateTime
    ) : ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>
{
  private readonly IdentityOptions _identityOptions = identityOptions.Value;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly UserTokenIssuer _userTokenIssuer = new(roleRepository, refreshTokenRepository, jwtTokenHandler, tokenFactory, machineDateTime, roleSettingsOptions.Value, refreshTokenSettingsOptions.Value);

  public async Task<AuthUserViewModel> CreateItemAsync(LoginUserCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var appName = command.RegisteredApp!.AppName;

    var user = (
            await _userManager.FindByNameAsync(command.UserName)
            ?? await _userManager.FindByEmailAsync(command.UserName)
            ) ?? throw new InvalidCredentialException("Unknown user name or email");

    if (await _userManager.IsLockedOutAsync(user))
      throw new InvalidCredentialException($"User {user.Id} is locked out");

    if (!await _userManager.CheckPasswordAsync(user, command.Password))
    {
      await _userManager.AccessFailedAsync(user);
      throw new InvalidCredentialException($"Wrong password for user {user.Id}");
    }

    await _userManager.ResetAccessFailedCountAsync(user);

    var userDto = await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
      ?? throw new InvalidCredentialException($"User {user.Id} has no profile for app {appName}");

    // We only include the accessToken in the payload, when user is confirmed.
    var isConfirmed = !_identityOptions.SignIn.RequireConfirmedAccount || user.EmailConfirmed || user.PhoneNumberConfirmed;
    AccessTokenViewModel? userToken = null;

    if (isConfirmed)
    {
      userToken = await _userTokenIssuer.StartSessionAsync(user.Id, user.UserName!, appName, cancellationToken);

      await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    return new AuthUserViewModel
    {
      User = userDto,
      UserToken = userToken,
      IsConfirmed = isConfirmed,
    };
  }
}
