using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Dtos;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record RegisterUserCommand : RegisterUserInput, IBaseCommand
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();
}

public record RegisterServiceUserCommand : RegisterUserInput, IBaseCommand
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;
}

public class RegisterUserCommandHandler(
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IInviteCodeRepository inviteCodeRepository,
    ITokenFactory tokenFactory,
    TimeProvider timeProvider,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler,
    ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel> createAccountLimitEventCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> createLoginTicketCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler
) : ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>, ICreateItemCommandHandler<RegisterServiceUserCommand, UserViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;

    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = createAccountLimitEventCommandHandler;

    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;

    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler =
        createLoginTicketCommandHandler;

    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        RegisterUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;
        var signInSettings = await GetSignInSettingsAsync(registeredApp, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            (var newUser, var inviteCodeEntity) = await PrepareUserAsync(command, registeredApp, signInSettings, cancellationToken);

            var existingUser = await FindExistingUserAsync(newUser);

            var vm = existingUser is null
                ? await SignUpAsync(command, signInSettings, newUser, inviteCodeEntity, now, cancellationToken)
                : await AnswerTakenSignUpAsync(command, signInSettings, existingUser, newUser, now, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return vm;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<UserViewModel> CreateItemAsync(
        RegisterServiceUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;
        var signInSettings = await GetSignInSettingsAsync(registeredApp, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            (var newUser, var inviteCodeEntity) = await PrepareUserAsync(command, registeredApp, signInSettings, cancellationToken);

            if (await FindExistingUserAsync(newUser) is not null)
                throw RecordExistsException.ForIdentifier(newUser.Email ?? command.UserName);

            var userDetails = await CreateAccountAsync(
                command,
                registeredApp,
                signInSettings,
                newUser,
                inviteCodeEntity,
                userId,
                now,
                cancellationToken
            );

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new UserViewModel { User = GetUserDto(newUser.Id, newUser, userDetails) };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<AppSignInSettingsDto> GetSignInSettingsAsync(RegisteredAppDto registeredApp, CancellationToken cancellationToken) =>
        (await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken)).SignInSettings;

    private async Task<(IdentityUser NewUser, InviteCodeEntity? InviteCode)> PrepareUserAsync(
        RegisterUserInput input,
        RegisteredAppDto registeredApp,
        AppSignInSettingsDto signInSettings,
        CancellationToken cancellationToken
    )
    {
        // A blank email would be stored as an empty string, and the unique index allows only one of those
        var userEmail = string.IsNullOrWhiteSpace(input.UserEmail) ? null : input.UserEmail;
        var phoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber;

        var hasInviteCode = !string.IsNullOrWhiteSpace(input.InviteCode);

        InviteCodeEntity? inviteCodeEntity = null;

        if (signInSettings.RequireInviteCode || hasInviteCode)
        {
            if (userEmail == null)
                throw new InvalidDataException(ErrorMessages.Users.EmailRequiredForInviteCode);

            if (!hasInviteCode)
                throw new InvalidDataException(ErrorMessages.Users.InviteCodeRequired);

            inviteCodeEntity = await _inviteCodeRepository.GetEntityByAlternateIdAsync(
                new() { AppId = registeredApp.Id, UserEmail = userEmail },
                QueryCondition.MayOrMayNotExist,
                cancellationToken
            );

            // A missing, wrong or used code gets the same answer, so sign-up never says whether an email has an invite
            if (
                inviteCodeEntity is null
                || inviteCodeEntity.Applied
                || !string.Equals(input.InviteCode, inviteCodeEntity.InviteCode, StringComparison.OrdinalIgnoreCase)
            )
                throw new InvalidDataException(ErrorMessages.Users.InviteCodeRequired);
        }

        var newUser = new IdentityUser
        {
            Email = userEmail,
            UserName = input.UserName,
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = false,
            EmailConfirmed = false,
        };

        await PasswordStrength.EnsureValidAsync(_userManager, newUser, input.UserPassword);

        newUser.PasswordHash = _userManager.PasswordHasher.HashPassword(newUser, input.UserPassword);

        return (newUser, inviteCodeEntity);
    }

    private async Task<IdentityUser?> FindExistingUserAsync(IdentityUser newUser) =>
        (newUser.Email is null ? null : await _userManager.FindByEmailAsync(newUser.Email))
        ?? await _userManager.FindByNameAsync(newUser.UserName!);

    private async Task<AuthUserViewModel> SignUpAsync(
        RegisterUserCommand command,
        AppSignInSettingsDto signInSettings,
        IdentityUser newUser,
        InviteCodeEntity? inviteCodeEntity,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var registeredApp = command.RegisteredApp;

        var userDetails = await CreateAccountAsync(
            command,
            registeredApp,
            signInSettings,
            newUser,
            inviteCodeEntity,
            newUser.Id,
            now,
            cancellationToken
        );

        if (!signInSettings.RequireConfirmedAccount)
            return await _signInUserCommandHandler.CreateItemAsync(
                new SignInUserCommand(newUser, registeredApp, command.RequestContext),
                newUser.Id,
                cancellationToken
            );

        await _createOutboxMessageCommandHandler.CreateItemAsync(
            new CreateOutboxMessageCommand(
                UserMessages.UserRegistered,
                UserMessages.Welcome(newUser, userDetails, registeredApp.AppName, command.RequestContext, now)
            ),
            newUser.Id,
            cancellationToken
        );

        var loginTicket = await _createLoginTicketCommandHandler.CreateItemAsync(
            new CreateLoginTicketCommand
            {
                UserId = newUser.Id,
                AppId = registeredApp.Id,
                Step = SignInStep.ConfirmEmail,
                CodeAction = UserMessages.WelcomeUserAction,
                CodeCorrelationId = newUser.Id,
            },
            newUser.Id,
            cancellationToken
        );

        return new AuthUserViewModel
        {
            User = GetUserDto(newUser.Id, newUser, userDetails),
            NextStep = SignInStep.ConfirmEmail,
            Ticket = loginTicket.Ticket,
            CodeSentTo = ContactMask.For(newUser.Email, newUser.PhoneNumber),
        };
    }

    private async Task<AuthUserViewModel> AnswerTakenSignUpAsync(
        RegisterUserCommand command,
        AppSignInSettingsDto signInSettings,
        IdentityUser existingUser,
        IdentityUser newUser,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        if (!signInSettings.RequireConfirmedAccount)
            throw new InvalidDataException(ErrorMessages.Users.AccountNotCreated);

        var notices = await _createAccountLimitEventCommandHandler.CreateItemAsync(
            new CreateAccountLimitEventCommand(existingUser.Id, AccountLimitKind.SignUpAttemptNotice),
            existingUser.Id,
            cancellationToken
        );

        if (!notices.IsReached)
        {
            var existingUserDetails = await _userRepository.GetUserDtoAsync(existingUser.Id, null, cancellationToken) ?? new UserDto();

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.SignUpAttempted,
                    UserMessages.SignUpAttempt(
                        existingUser,
                        existingUserDetails,
                        command.RegisteredApp.AppName,
                        command.RequestContext,
                        now
                    )
                ),
                existingUser.Id,
                cancellationToken
            );
        }

        var unsavedUserDetails = new UserEntity
        {
            Id = newUser.Id,
            FirstName = command.FirstName,
            LastName = command.LastName,
        };

        unsavedUserDetails.SetEntityAuditFields(newUser.Id, now);

        return new AuthUserViewModel
        {
            User = GetUserDto(newUser.Id, newUser, unsavedUserDetails),
            NextStep = SignInStep.ConfirmEmail,
            Ticket = _tokenFactory.GenerateToken(),
            CodeSentTo = ContactMask.For(newUser.Email, newUser.PhoneNumber),
        };
    }

    private async Task<UserEntity> CreateAccountAsync(
        RegisterUserInput input,
        RegisteredAppDto registeredApp,
        AppSignInSettingsDto signInSettings,
        IdentityUser newUser,
        InviteCodeEntity? inviteCodeEntity,
        string createdBy,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var userDefaultRole = AppRoleNames.DefaultRole(registeredApp.AppName, signInSettings);

        if (userDefaultRole is not null && await _roleManager.FindByNameAsync(userDefaultRole) == null)
            if (!(await _roleManager.CreateAsync(new IdentityRole { Name = userDefaultRole })).Succeeded)
                throw new RecordCreationException($"Role {userDefaultRole} wasn't created");

        var identityResult = await _userManager.CreateAsync(newUser);

        if (!identityResult.Succeeded)
        {
            var errors = string.Join(" ", identityResult.Errors.Select(e => e.Description));
            throw new InvalidDataException(errors);
        }

        List<string> roleNames = userDefaultRole is null ? [] : [userDefaultRole];

        if (inviteCodeEntity?.Role is { } inviteRole && !string.Equals(inviteRole, userDefaultRole, StringComparison.OrdinalIgnoreCase))
        {
            var role =
                await _roleManager.FindByNameAsync(inviteRole)
                ?? throw new InvalidDataException(ErrorMessages.Users.InviteRoleMissing(inviteRole));

            roleNames.Add(role.Name!);
        }

        if (roleNames.Count > 0 && !(await _userManager.AddToRolesAsync(newUser, roleNames)).Succeeded)
            throw new RecordCreationException($"Roles {string.Join(", ", roleNames)} weren't added to user {newUser.Id}");

        var userDetails = new UserEntity
        {
            Id = newUser.Id,
            FirstName = input.FirstName,
            LastName = input.LastName,
        };

        userDetails.SetEntityAuditFields(createdBy, now);

        _userRepository.CreateEntityItem(userDetails);

        if (inviteCodeEntity != null)
        {
            inviteCodeEntity.Applied = true;
            inviteCodeEntity.UpdateEntityAuditFields(createdBy, now);

            _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity);
        }

        var userRegisteredApp = new UserRegisteredAppEntity { UserId = newUser.Id, AppId = registeredApp.Id };

        userRegisteredApp.SetEntityAuditFields(createdBy, now);

        _userRegisteredAppRepository.CreateEntityItem(userRegisteredApp);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return userDetails;
    }

    private static UserDto GetUserDto(string id, IdentityUser user, UserEntity userDetails)
    {
        var userDto = new UserDto
        {
            Id = id,
            UserName = user.UserName,
            FirstName = userDetails.FirstName,
            LastName = userDetails.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
        };

        userDto.SetDtoAuditFields(userDetails);

        return userDto;
    }
}
