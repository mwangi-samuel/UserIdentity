using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record RegisterUserCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public RequestSource RequestSource { get; internal set; } = RequestSource.UI;

    [JsonIgnore]
    public ActiveRegisteredApp? RegisteredApp { get; internal set; }

    [JsonIgnore]
    public CrudEvent Event { get; internal set; } = CrudEvent.Create;

    [Required, StringLength(UserLimits.FirstNameMaxLength)]
    public string FirstName { get; init; } = null!;

    [StringLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; init; }

    [Required, StringLength(UserLimits.UserNameMaxLength)]
    public string UserName { get; init; } = null!;

    [StringLength(UserLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; init; }

    [StringLength(UserLimits.EmailMaxLength), RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)]
    public string? UserEmail { get; init; }

    [Required]
    public string UserPassword { get; init; } = null!;

    public string? GoogleRecaptchaToken { get; set; }

    public string? InviteCode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) && string.IsNullOrWhiteSpace(UserEmail))
            yield return new ValidationResult(ErrorMessages.Users.PhoneNumberOrEmailRequired, [nameof(PhoneNumber), nameof(UserEmail)]);

        // Login and reset look up the user name first, so an email-like user name would shadow that email's real account
        if (UserName?.Contains('@') == true && !string.Equals(UserName, UserEmail, StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult(ErrorMessages.Users.UserNameCannotBeAnotherEmail, [nameof(UserName)]);
    }
}

public class RegisterUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<IdentityOptions> identityOptions,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IInviteCodeRepository inviteCodeRepository,
    TimeProvider timeProvider,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    ICreateItemCommandHandler<StartSessionCommand, AccessTokenViewModel> startSessionCommandHandler,
    ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> createLoginTicketCommandHandler,
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

    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;

    private readonly ICreateItemCommandHandler<StartSessionCommand, AccessTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;

    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler =
        createLoginTicketCommandHandler;

    private readonly IGoogleRecaptchaService _googleRecaptchaService = googleRecaptchaService;

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

        // A blank email would be stored as an empty string, and the unique index allows only one of those
        var userEmail = string.IsNullOrWhiteSpace(command.UserEmail) ? null : command.UserEmail;
        var phoneNumber = string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber;

        var userDefaultRole = AppRoleNames.DefaultRole(registeredAppName, _roleSettings);

        // Start DB Transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Check if invite code is required for current app
        var inviteCodeRequired = command.RegisteredApp?.RequireInviteCode ?? false;
        var hasInviteCode = !string.IsNullOrWhiteSpace(command.InviteCode);

        InviteCodeEntity? inviteCodeEntity = null;

        if (inviteCodeRequired || hasInviteCode)
        {
            if (userEmail == null)
                throw new InvalidDataException(ErrorMessages.Users.EmailRequiredForInviteCode);

            if (!hasInviteCode)
                throw new InvalidDataException(ErrorMessages.Users.InviteCodeRequired);

            inviteCodeEntity = await _inviteCodeRepository.GetEntityByAlternateIdAsync(
                new() { AppId = command.RegisteredApp!.Id, UserEmail = userEmail },
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
        if (userEmail != null && await _userManager.FindByEmailAsync(userEmail) != null)
            throw RecordExistsException.ForIdentifier(userEmail);

        var newUser = new IdentityUser
        {
            Email = userEmail,
            UserName = command.UserName,
            PhoneNumber = phoneNumber,
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

            var isConfirmed = AccountConfirmation.IsConfirmed(_identityOptions, newUser);

            var userToken = isConfirmed
                ? await _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(newUser.Id, command.UserName, registeredAppName),
                    newUser.Id,
                    cancellationToken
                )
                : null;

            var loginTicket =
                !isConfirmed && command.RequestSource == RequestSource.UI
                    ? await _createLoginTicketCommandHandler.CreateItemAsync(
                        new CreateLoginTicketCommand(newUser.Id, command.RegisteredApp.Id),
                        newUser.Id,
                        cancellationToken
                    )
                    : null;

            var welcomeMessage = UserMessages.Welcome(newUser, userDetails, registeredAppName);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(UserMessages.UserRegistered, welcomeMessage),
                newUser.Id,
                cancellationToken
            );

            if (command.RequestSource == RequestSource.Api && command.RegisteredApp.CallbackUrl != null)
                await _createOutboxMessageCommandHandler.CreateItemAsync(
                    new CreateOutboxMessageCommand(UserMessages.CallbackRequested, welcomeMessage),
                    newUser.Id,
                    cancellationToken
                );

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
                LoginTicket = loginTicket?.Ticket,
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
