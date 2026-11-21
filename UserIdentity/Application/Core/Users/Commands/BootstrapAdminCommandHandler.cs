using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Persistence.Repositories.RegisteredApps;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record BootstrapAdminCommand : IBaseCommand;

public class BootstrapAdminCommandHandler(
    IOptions<BootstrapAdminSettings> bootstrapAdminSettings,
    IOptions<RoleSettings> roleSettings,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IRegisteredAppRepository registeredAppRepository,
    ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel> createRegisteredAppCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<BootstrapAdminCommand, BootstrappedAdminViewModel>
{
    private readonly BootstrapAdminSettings _bootstrapAdminSettings = bootstrapAdminSettings.Value;
    private readonly RoleSettings _roleSettings = roleSettings.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IRegisteredAppRepository _registeredAppRepository = registeredAppRepository;
    private readonly ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel> _createRegisteredAppCommandHandler =
        createRegisteredAppCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<BootstrappedAdminViewModel> CreateItemAsync(
        BootstrapAdminCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var settings = _bootstrapAdminSettings;
        var globalAdminRole = _roleSettings.GlobalAdminRole.TrimAndLowerInvariant();

        if (!settings.IsEnabled || (await _userManager.GetUsersInRoleAsync(globalAdminRole)).Count > 0)
            return new BootstrappedAdminViewModel();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await _userManager.FindByNameAsync(settings.UserName!) is not null)
                throw RecordExistsException.ForIdentifier(settings.UserName!);

            if (await _userManager.FindByEmailAsync(settings.Email!) is not null)
                throw RecordExistsException.ForIdentifier(settings.Email!);

            var admin = new IdentityUser
            {
                UserName = settings.UserName,
                Email = settings.Email,
                EmailConfirmed = true,
                PhoneNumber = string.IsNullOrWhiteSpace(settings.PhoneNumber) ? null : settings.PhoneNumber,
                PhoneNumberConfirmed = false,
            };

            var identityResult = await _userManager.CreateAsync(admin, settings.Password!);

            if (!identityResult.Succeeded)
                throw new InvalidDataException(string.Join(" ", identityResult.Errors.Select(e => e.Description)));

            var appId = await GetOrCreateAppIdAsync(admin.Id, cancellationToken);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var userDetails = new UserEntity
            {
                Id = admin.Id,
                FirstName = settings.FirstName!,
                LastName = settings.LastName!,
            };
            userDetails.SetEntityAuditFields(admin.Id, now);
            _userRepository.CreateEntityItem(userDetails);

            var userRegisteredApp = new UserRegisteredAppEntity { UserId = admin.Id, AppId = appId };
            userRegisteredApp.SetEntityAuditFields(admin.Id, now);
            _userRegisteredAppRepository.CreateEntityItem(userRegisteredApp);

            List<string> roleNames = [globalAdminRole, $"{AppRoleNames.Prefix(settings.AppName!)}{KnownRoles.Admin}"];

            foreach (var roleName in roleNames)
                if (
                    await _roleManager.FindByNameAsync(roleName) is null
                    && !(await _roleManager.CreateAsync(new IdentityRole(roleName))).Succeeded
                )
                    throw new RecordCreationException($"Role {roleName} wasn't created");

            if (!(await _userManager.AddToRolesAsync(admin, roleNames)).Succeeded)
                throw new RecordCreationException($"Roles {string.Join(", ", roleNames)} weren't added to user {admin.Id}");

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new BootstrappedAdminViewModel { UserId = admin.Id };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<int> GetOrCreateAppIdAsync(string adminId, CancellationToken cancellationToken)
    {
        var settings = _bootstrapAdminSettings;

        var registeredApp = await _registeredAppRepository.GetEntityByAlternateIdAsync(
            new RegisteredAppEntity { AppName = settings.AppName! },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        if (registeredApp is not null)
            return registeredApp.Id;

        var createdApp = await _createRegisteredAppCommandHandler.CreateItemAsync(
            new CreateRegisteredAppCommand { AppName = settings.AppName!, BaseUrl = settings.AppBaseUrl! },
            adminId,
            cancellationToken
        );

        return createdApp.RegisteredApp.Id;
    }
}
