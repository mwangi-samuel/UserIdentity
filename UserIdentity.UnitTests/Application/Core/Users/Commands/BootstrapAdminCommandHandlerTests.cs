using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Persistence.Repositories.RegisteredApps;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class BootstrapAdminCommandHandlerTests
{
    private const string GlobalAdminRole = "global:admin";
    private const string AppAdminRole = "bootstrapapp:admin";

    private readonly DateTime _fixedNow = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly RoleManager<IdentityRole> _roleManager = A.Fake<RoleManager<IdentityRole>>();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IRegisteredAppRepository _registeredAppRepository = A.Fake<IRegisteredAppRepository>();
    private readonly ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel> _createRegisteredAppCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel>>();
    private readonly FakeTimeProvider _timeProvider = new();

    private readonly BootstrapAdminSettings _settings = new()
    {
        AppName = "BootstrapApp",
        AppBaseUrl = "https://bootstrap.test",
        UserName = "bootstrap.admin",
        FirstName = "Boot",
        Email = "bootstrap.admin@example.com",
        Password = "Adm1n@P@ss",
    };

    public BootstrapAdminCommandHandlerTests()
    {
        _timeProvider.SetUtcNow(_fixedNow);

        A.CallTo(() => _userManager.GetUsersInRoleAsync(GlobalAdminRole)).Returns([]);
        A.CallTo(() => _userManager.FindByNameAsync(A<string>._)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(A<string>._)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);
        A.CallTo(() => _roleManager.FindByNameAsync(A<string>._)).Returns((IdentityRole?)null);
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>._)).Returns(IdentityResult.Success);
        A.CallTo(() =>
                _registeredAppRepository.GetEntityByAlternateIdAsync(
                    A<RegisteredAppEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns((RegisteredAppEntity?)null);
        A.CallTo(() =>
                _createRegisteredAppCommandHandler.CreateItemAsync(A<CreateRegisteredAppCommand>._, A<string>._, A<CancellationToken>._)
            )
            .Returns(
                new RegisteredAppViewModel
                {
                    RegisteredApp = new RegisteredAppDto { Id = 5, AppName = "BootstrapApp" },
                }
            );
    }

    [Fact]
    public async Task CreateItemAsync_Does_Nothing_While_Disabled()
    {
        // Act
        var vm = await CreateHandler(new BootstrapAdminSettings())
            .CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(vm.UserId);
        A.CallTo(() => _userManager.GetUsersInRoleAsync(A<string>._)).MustNotHaveHappened();
        AssertNothingCreated();
    }

    [Fact]
    public async Task CreateItemAsync_Does_Nothing_When_A_Global_Admin_Exists()
    {
        // Arrange
        A.CallTo(() => _userManager.GetUsersInRoleAsync(GlobalAdminRole)).Returns([new() { Id = "admin-1" }]);

        // Act
        var vm = await CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(vm.UserId);
        AssertNothingCreated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateItemAsync_With_A_Taken_User_Name_Or_Email_Throws_RecordExistsException_And_Rolls_Back(bool userNameTaken)
    {
        // Arrange
        if (userNameTaken)
            A.CallTo(() => _userManager.FindByNameAsync(_settings.UserName!)).Returns(new IdentityUser());
        else
            A.CallTo(() => _userManager.FindByEmailAsync(_settings.Email!)).Returns(new IdentityUser());

        // Act & Assert
        await Assert.ThrowsAsync<RecordExistsException>(() =>
            CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateItemAsync_When_The_User_Cannot_Be_Created_Throws_InvalidDataException_And_Rolls_Back()
    {
        // Arrange
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, _settings.Password!))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Passwords must be at least 8 characters." }));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken)
        );

        Assert.Equal("Passwords must be at least 8 characters.", exception.Message);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateItemAsync_Creates_A_Confirmed_Admin_And_Its_App_Without_Required_Confirmation_When_The_App_Is_Missing()
    {
        // Arrange
        IdentityUser? createdUser = null;
        UserRegisteredAppEntity? userRegisteredApp = null;

        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, _settings.Password!))
            .Invokes((IdentityUser user, string _) => createdUser = user)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userRegisteredAppRepository.CreateEntityItem(A<UserRegisteredAppEntity>._))
            .Invokes((UserRegisteredAppEntity entity) => userRegisteredApp = entity);

        // Act
        var vm = await CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(createdUser);
        Assert.Equal(createdUser.Id, vm.UserId);
        Assert.Equal(_settings.UserName, createdUser.UserName);
        Assert.Equal(_settings.Email, createdUser.Email);
        Assert.True(createdUser.EmailConfirmed);

        A.CallTo(() =>
                _createRegisteredAppCommandHandler.CreateItemAsync(
                    A<CreateRegisteredAppCommand>.That.Matches(command =>
                        command.AppName == _settings.AppName && command.BaseUrl == _settings.AppBaseUrl && command.AppSecretKey == null
                    ),
                    createdUser.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        Assert.NotNull(userRegisteredApp);
        Assert.Equal(createdUser.Id, userRegisteredApp.UserId);
        Assert.Equal(5, userRegisteredApp.AppId);
        A.CallTo(() =>
                _userRepository.CreateEntityItem(
                    A<UserEntity>.That.Matches(user =>
                        user.Id == createdUser.Id && user.FirstName == _settings.FirstName && user.CreatedAt == _fixedNow
                    )
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateItemAsync_Links_The_Admin_To_An_Existing_App_Without_Creating_One()
    {
        // Arrange
        UserRegisteredAppEntity? userRegisteredApp = null;

        A.CallTo(() =>
                _registeredAppRepository.GetEntityByAlternateIdAsync(
                    A<RegisteredAppEntity>.That.Matches(app => app.AppName == _settings.AppName),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(new RegisteredAppEntity { Id = 3, AppName = "BootstrapApp" });
        A.CallTo(() => _userRegisteredAppRepository.CreateEntityItem(A<UserRegisteredAppEntity>._))
            .Invokes((UserRegisteredAppEntity entity) => userRegisteredApp = entity);

        // Act
        await CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(userRegisteredApp);
        Assert.Equal(3, userRegisteredApp.AppId);
        A.CallTo(() =>
                _createRegisteredAppCommandHandler.CreateItemAsync(A<CreateRegisteredAppCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateItemAsync_Creates_The_Missing_Roles_And_Grants_The_Global_Admin_And_App_Admin_Roles()
    {
        // Arrange
        A.CallTo(() => _roleManager.FindByNameAsync(GlobalAdminRole)).Returns(new IdentityRole(GlobalAdminRole));

        // Act
        await CreateHandler().CreateItemAsync(new BootstrapAdminCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(role => role.Name == AppAdminRole)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>._)).MustHaveHappenedOnceExactly();
        string[] expectedRoles = [GlobalAdminRole, AppAdminRole];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    private void AssertNothingCreated()
    {
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() =>
                _createRegisteredAppCommandHandler.CreateItemAsync(A<CreateRegisteredAppCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
    }

    private BootstrapAdminCommandHandler CreateHandler(BootstrapAdminSettings? settings = null) =>
        new(
            Options.Create(settings ?? _settings),
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _roleManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _registeredAppRepository,
            _createRegisteredAppCommandHandler,
            _timeProvider
        );
}
