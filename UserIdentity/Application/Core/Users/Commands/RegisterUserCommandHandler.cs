using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record RegisterUserCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public RequestSource RequestSource { get; internal set; } = RequestSource.UI;

    [JsonIgnore]
    public RegisteredAppEntity? RegisteredApp { get; internal set; }

    [JsonIgnore]
    public CrudEvent Event { get; internal set; } = CrudEvent.Create;

    [Required]
    public string FirstName { get; init; } = null!;

    public string? LastName { get; init; }

    [Required]
    public string UserName { get; init; } = null!;

    public string? PhoneNumber { get; init; }

    public string? UserEmail { get; init; }

    [Required]
    public string UserPassword { get; init; } = null!;

    public string? GoogleRecaptchaToken { get; set; }

    public string? InviteCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) && string.IsNullOrWhiteSpace(UserEmail))
            yield return new ValidationResult(ErrorMessages.Users.PhoneNumberOrEmailRequired, [nameof(PhoneNumber), nameof(UserEmail)]);
    }
}

public class RegisterUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<IdentityOptions> identityOptions,
    IOptions<RefreshTokenSettings> refreshTokenSettingsOptions,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IInviteCodeRepository inviteCodeRepository,
    IRoleRepository roleRepository,
    IJwtTokenHandler jwtTokenHandler,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider,
    IMessageOutbox messageOutbox,
    IGoogleRecaptchaService googleRecaptchaService
) : ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

    private readonly TimeProvider _timeProvider = timeProvider;

    private readonly IMessageOutbox _messageOutbox = messageOutbox;

    private readonly IGoogleRecaptchaService _googleRecaptchaService = googleRecaptchaService;

    private readonly UserTokenIssuer _userTokenIssuer = new(
        roleRepository,
        refreshTokenRepository,
        jwtTokenHandler,
        tokenFactory,
        timeProvider,
        roleSettingsOptions.Value,
        refreshTokenSettingsOptions.Value
    );

    public async Task<AuthUserViewModel> CreateItemAsync(
        RegisterUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        // Confirm Google Recaptcha Token is valid before processing
        if (
            command.RequestSource == RequestSource.UI
            && !await _googleRecaptchaService.VerifyTokenAsync(command.GoogleRecaptchaToken, cancellationToken)
        )
            throw new InvalidDataException(ErrorMessages.Users.InvalidRecaptchaToken);

        var registeredAppName = command.RegisteredApp!.AppName;

        var userDefaultRole = AppRoleNames.DefaultRole(registeredAppName, _roleSettings);

        // Start DB Transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Check if invite code is required for current app
        var inviteCodeRequired = command.RegisteredApp?.RequireInviteCode ?? false;
        var hasInviteCode = !string.IsNullOrWhiteSpace(command.InviteCode);

        InviteCodeEntity? inviteCodeEntity = null;

        if (inviteCodeRequired || hasInviteCode)
        {
            if (command.UserEmail == null)
                throw new InvalidDataException(ErrorMessages.Users.EmailRequiredForInviteCode);

            if (!hasInviteCode)
                throw new InvalidDataException(ErrorMessages.Users.InviteCodeRequired);

            inviteCodeEntity = await _inviteCodeRepository.GetEntityByAlternateIdAsync(
                new() { AppId = command.RegisteredApp!.Id, UserEmail = command.UserEmail! },
                QueryCondition.MayOrMayNotExist,
                cancellationToken
            );

            // A missing, wrong or used code gets the same answer, so sign-up never says whether an email has an invite
            if (
                inviteCodeEntity is null
                || inviteCodeEntity.Applied
                || !string.Equals(command.InviteCode, inviteCodeEntity.InviteCode, StringComparison.OrdinalIgnoreCase)
            )
                throw new InvalidDataException(ErrorMessages.Users.InviteCodeRequired);
        }

        //  Check if default role is created otherwise create
        if (await _roleManager.FindByNameAsync(userDefaultRole) == null)
            if (!(await _roleManager.CreateAsync(new IdentityRole { Name = userDefaultRole })).Succeeded)
                throw new RecordCreationException($"Role {userDefaultRole} wasn't created");

        // Check if user exists by user name, throw RecordExistsException
        if (await _userManager.FindByNameAsync(command.UserName) != null)
            throw RecordExistsException.ForIdentifier(command.UserName);

        // Check if user exists by user email, throw RecordExistsException
        if (command.UserEmail != null && await _userManager.FindByEmailAsync(command.UserEmail) != null)
            throw RecordExistsException.ForIdentifier(command.UserEmail);

        var newUser = new IdentityUser
        {
            Email = command.UserEmail,
            UserName = command.UserName,
            PhoneNumber = command.PhoneNumber,
            PhoneNumberConfirmed = false,
            EmailConfirmed = false,
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
                throw new RecordCreationException($"Role {userDefaultRole} wasn't added to user {newUser.Id}");

            if (inviteCodeEntity?.Role is { } inviteRole && !string.Equals(inviteRole, userDefaultRole, StringComparison.OrdinalIgnoreCase))
            {
                var role =
                    await _roleManager.FindByNameAsync(inviteRole)
                    ?? throw new InvalidDataException(ErrorMessages.Users.InviteRoleMissing(inviteRole));

                if (!(await _userManager.AddToRoleAsync(newUser, role.Name!)).Succeeded)
                    throw new RecordCreationException($"Role {role.Name} wasn't added to user {newUser.Id}");
            }

            // Create user details
            var userDetails = new UserEntity
            {
                Id = newUser.Id,
                FirstName = command.FirstName,
                LastName = command.LastName,
                IsDeleted = false,
            };

            userDetails.SetEntityAuditFields(newUser.Id, _timeProvider.GetUtcNow().UtcDateTime);

            _userRepository.CreateEntityItem(userDetails);

            // Mark the invite code as applied by the new user
            if (inviteCodeEntity != null)
            {
                inviteCodeEntity.Applied = true;
                inviteCodeEntity.UpdateEntityAuditFields(newUser.Id, _timeProvider.GetUtcNow().UtcDateTime);

                _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity); // No worries - we are using a db transaction
            }

            // Create User Registered App Mapping
            var userRegisteredApp = new UserRegisteredAppEntity
            {
                Id = $"{newUser.Id}-{command.RegisteredApp!.Id}",
                UserId = newUser.Id,
                AppId = command.RegisteredApp!.Id,
            };

            userRegisteredApp.SetEntityAuditFields(newUser.Id, _timeProvider.GetUtcNow().UtcDateTime);

            _userRegisteredAppRepository.CreateEntityItem(userRegisteredApp);

            // We only include the accessToken in the payload, when user is confirmed.
            var isConfirmed = !_identityOptions.SignIn.RequireConfirmedAccount || newUser.EmailConfirmed || newUser.PhoneNumberConfirmed;

            var userToken = isConfirmed
                ? await _userTokenIssuer.StartSessionAsync(newUser.Id, command.UserName, registeredAppName, cancellationToken)
                : null;

            var welcomeMessage = UserMessages.Welcome(newUser, userDetails, registeredAppName);

            _messageOutbox.Add(UserMessages.UserUpdated, welcomeMessage);

            if (command.RequestSource == RequestSource.UI && command.RegisteredApp.CallbackUrl != null)
                _messageOutbox.Add(UserMessages.CallbackRequested, welcomeMessage);

            // Commit DB Transaction
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Return registration results
            var userDto = new UserDto
            {
                Id = newUser.Id,
                UserName = newUser.UserName,
                FirstName = userDetails.FirstName,
                LastName = userDetails.LastName,
                Email = newUser.Email,
                PhoneNumber = newUser.PhoneNumber,
            };

            userDto.SetDtoAuditFields(userDetails);

            return new AuthUserViewModel
            {
                User = userDto,
                IsConfirmed = isConfirmed,
                UserToken = userToken,
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
