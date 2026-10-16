using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Attributes;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entity;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Enums;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record RegisterUserCommand : IBaseCommand
{
  [JsonIgnore]
  public RequestSource RequestSource { get; internal set; } = RequestSource.UI;

  [JsonIgnore]
  public RegisteredAppEntity? RegisteredApp { get; internal set; }

  [JsonIgnore]
  public CrudEvent Event { get; internal set; } = CrudEvent.CREATE;

  [Required]
  public string FirstName { get; init; } = null!;

  public string? LastName { get; init; }

  [Required]
  public string UserName { get; init; } = null!;

  [EitherOr(nameof(PhoneNumber), nameof(UserEmail))]
  public string? PhoneNumber { get; init; }

  [EitherOr(nameof(PhoneNumber), nameof(UserEmail))]
  public string? UserEmail { get; init; }

  [Required]
  public string UserPassword { get; init; } = null!;

  public string? GoogleRecaptchaToken { get; set; }

  public string? InviteCode { get; set; }
}

public class RegisterUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<IdentityOptions> identityOptions,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IInviteCodeRepository inviteCodeRepository,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    IMachineDateTime machineDateTime,
    IEventHandler<UserUpdateEvent> userUpdateEventHandler,
    IOptions<GoogleRecaptchaSettings> googleRecaptchaSettingsOptions,
    IGoogleRecaptchaService googleRecaptchaService,
    IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> getRoleClaimsQueryHandler
    ) : ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>
{
  private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
  private readonly IdentityOptions _identityOptions = identityOptions.Value;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  private readonly IUnitOfWork _unitOfWork = unitOfWork;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory = tokenFactory;

  private readonly IMachineDateTime _machineDateTime = machineDateTime;

  private readonly IEventHandler<UserUpdateEvent> _userUpdateEventHandler = userUpdateEventHandler;

  private readonly GoogleRecaptchaSettings _googleRecaptchaSettings = googleRecaptchaSettingsOptions.Value;
  private readonly IGoogleRecaptchaService _googleRecaptchaService = googleRecaptchaService;

  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler = getRoleClaimsQueryHandler;

  public async Task<AuthUserViewModel> CreateItemAsync(RegisterUserCommand command, string userId, CancellationToken cancellationToken = default)
  {

    // Confirm Google Recaptcha Token is valid before processing
    if (_googleRecaptchaSettings.Enabled && command.RequestSource == RequestSource.UI)
    {
      var googleRecaptchaToken = ObjectUtil.RequireNonNullValue(command.GoogleRecaptchaToken, nameof(command.GoogleRecaptchaToken), $"{nameof(command.GoogleRecaptchaToken)} is required");

      if (!await _googleRecaptchaService.VerifyTokenAsync(googleRecaptchaToken, cancellationToken))
        throw new InvalidDataException($"Invalid {nameof(command.GoogleRecaptchaToken)} provided");
    }

    var registeredAppName = command.RegisteredApp!.AppName;

    var userDefaultRole = ResolveDefaultRole(registeredAppName);

    // Start DB Transaction
    await _unitOfWork.BeginTransactionAsync(cancellationToken);

    // Check if invite code is required for current app
    var inviteCodeRequired = command.RegisteredApp?.RequireInviteCode ?? false;

    InviteCodeEntity inviteCodeEntity;

    if (inviteCodeRequired)
    {
      if (command.UserEmail == null)
        throw new InvalidDataException("An email is required when signing up with an invite code.");

      if (command.InviteCode == null)
        throw new InvalidDataException("A valid invite code is required when signing up.");

      inviteCodeEntity = (await _inviteCodeRepository.GetEntityByAlternateIdAsync(new() { UserEmail = command.UserEmail! }, QueryCondition.MUST_EXIST, cancellationToken))!;

      if (!string.Equals(command.InviteCode, inviteCodeEntity.InviteCode, StringComparison.OrdinalIgnoreCase) || inviteCodeEntity.Applied)
        throw new InvalidDataException("A valid invite code is required when signing up.");

      inviteCodeEntity.Applied = true;

      _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity); // No worries - we are using a db transaction
    }

    //  Check if default role is created otherwise create 
    if (await _roleManager.FindByNameAsync(userDefaultRole) == null)
      if (!(await _roleManager.CreateAsync(new IdentityRole { Name = userDefaultRole })).Succeeded)
        throw new RecordCreationException(userDefaultRole, EntityTypes.ROLE.Description());

    // Check if user exists by user name, throw RecordExistsException 
    if (await _userManager.FindByNameAsync(command.UserName) != null)
      throw new RecordExistsException(command.UserName, EntityTypes.USER.Description());

    // Check if user exists by user email, throw RecordExistsException
    if (command.UserEmail != null && await _userManager.FindByEmailAsync(command.UserEmail) != null)
      throw new RecordExistsException(command.UserEmail, EntityTypes.USER.Description());

    var newUser = new IdentityUser
    {
      Email = command.UserEmail,
      UserName = command.UserName,
      PhoneNumber = command.PhoneNumber,
      PhoneNumberConfirmed = false,
      EmailConfirmed = false
    };

    try
    {
      var identityResult = await _userManager.CreateAsync(newUser, command.UserPassword);

      if (!identityResult.Succeeded)
      {
        var errors = string.Join(" ", identityResult.Errors.Select(e => e.Description));
        throw new InvalidDataException(errors);
      }

      // User vs Default Role
      var resultUserRole = await _userManager.AddToRoleAsync(newUser, userDefaultRole);

      if (!resultUserRole.Succeeded)
        throw new RecordCreationException(userDefaultRole, EntityTypes.ROLE.Description());


      // Create user details
      var emailTokenBytes = Encoding.UTF8.GetBytes(await _userManager.GenerateEmailConfirmationTokenAsync(newUser));
      var confirmEmailToken = WebEncoders.Base64UrlEncode(emailTokenBytes);

      var userDetails = new UserEntity
      {
        Id = newUser.Id,
        FirstName = command.FirstName,
        LastName = command.LastName,
        EmailConfirmationToken = confirmEmailToken,
        IsDeleted = false
      };

      userDetails.SetEntityAuditFields(newUser.Id, _machineDateTime.Now);

      _userRepository.CreateEntityItem(userDetails);

      // Create User Registered App Mapping
      var userRegisteredApp = new UserRegisteredAppEntity
      {
        Id = $"{newUser.Id}-{command.RegisteredApp!.Id}",
        UserId = newUser.Id,
        AppId = command.RegisteredApp!.Id
      };

      userRegisteredApp.SetEntityAuditFields(newUser.Id, _machineDateTime.Now);

      _userRegisteredAppRepository.CreateEntityItem(userRegisteredApp);


      var userRoles = new List<string> { userDefaultRole };
      var roleClaimsVm = await _getRoleClaimsQueryHandler.GetItemsAsync(new GetRoleClaimsForRolesQuery { Roles = userRoles }, cancellationToken);


      // We only include the accessToken in the payload, when user is confirmed.
      var isConfirmed = !_identityOptions.SignIn.RequireConfirmedAccount || newUser.EmailConfirmed || newUser.PhoneNumberConfirmed;
      AccessTokenViewModel? userToken = null;

      if (isConfirmed)
      {
        // Generate access Token
        (var token, var expiresIn) = _jwtTokenHandler.CreateToken(newUser.Id, newUser.UserName, registeredAppName, [.. userRoles], roleClaimsVm.RoleClaims);


        //Generate and save Refresh Token details
        var refreshToken = _tokenFactory.GenerateToken();

        var userRefreshToken = new RefreshTokenEntity
        {
          UserId = newUser.Id,
          Expires = _machineDateTime.Now.AddSeconds(expiresIn),
          Token = refreshToken
        };

        userRefreshToken.SetEntityAuditFields(newUser.Id, _machineDateTime.Now);

        _refreshTokenRepository.CreateEntityItem(userRefreshToken);

        userToken = new AccessTokenViewModel
        {
          AccessToken = new AccessTokenDTO
          {
            Token = token,
            ExpiresIn = expiresIn
          },
          RefreshToken = refreshToken,
        };

      }

      // Commit DB Transaction 
      await _unitOfWork.CommitTransactionAsync(cancellationToken);

      // Publish User Created Event
      _ = HandleEventAsync(userDetails, newUser, command.RegisteredApp, command.RequestSource);

      // Return registration results
      return new AuthUserViewModel
      {
        User = new UserDTO
        {
          Id = newUser.Id,
          UserName = newUser.UserName,
          FirstName = userDetails.FirstName,
          LastName = userDetails.LastName,
          Email = newUser.Email,
          PhoneNumber = newUser.PhoneNumber,
          Roles = [.. userRoles],
          RoleClaims = roleClaimsVm.RoleClaims,
          CreatedAt = userDetails.CreatedAt,
          CreatedBy = userDetails.CreatedBy,
          UpdatedAt = userDetails.UpdatedAt,
          UpdatedBy = userDetails.UpdatedBy
        },
        IsConfirmed = isConfirmed,
        UserToken = userToken
      };
    }
    catch
    {
      await _unitOfWork.RollbackTransactionAsync();
      throw;
    }
  }

  private string ResolveDefaultRole(string appName)
  {
    return $"{appName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR}{_roleSettings.DefaultRole.TrimAndLowerInvariant()}";
  }

  private async Task HandleEventAsync(UserEntity userEntity, IdentityUser identityUser, RegisteredAppEntity registeredAppEntity, RequestSource requestSource)
  {
    var eventItem = new UserUpdateEvent
    {
      EventType = CrudEvent.CREATE,
      UserContent = new UserEventContent
      {
        UserIdentityId = identityUser.Id,
        FirstName = userEntity.FirstName,
        LastName = userEntity.LastName,
        UserName = identityUser.UserName,
        PhoneNumber = identityUser.PhoneNumber,
        PhoneNumberConfirmed = identityUser.PhoneNumberConfirmed,
        UserEmail = identityUser.Email,
        EmailConfirmed = identityUser.EmailConfirmed
      },
      RegisteredApp = registeredAppEntity,
      RequestSource = requestSource
    };

    await _userUpdateEventHandler.HandleEventAsync(eventItem);
  }
}
