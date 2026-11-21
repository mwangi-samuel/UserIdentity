using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Queries;

public class GetUserByEmailQueryHandlerTests
{
    private const string Email = "user@test.com";

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1", Email = Email };
    private readonly UserDto _userDto = new() { Id = "user-1", Email = Email };

    public GetUserByEmailQueryHandlerTests()
    {
        A.CallTo(() => _userManager.FindByEmailAsync(Email)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(_userDto);
    }

    [Fact]
    public async Task An_Account_Of_The_App_Is_Found_By_Its_Email()
    {
        // Act
        var vm = await GetHandler().GetItemAsync(GetQuery(isGlobalAdmin: false), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(_userDto, vm.User);
    }

    [Fact]
    public async Task An_Unknown_Email_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userManager.FindByEmailAsync(Email)).Returns((IdentityUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().GetItemAsync(GetQuery(isGlobalAdmin: false), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task An_Account_Of_Another_App_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().GetItemAsync(GetQuery(isGlobalAdmin: true), TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData("testapp:admin")]
    [InlineData("global:admin")]
    public async Task An_Admin_Account_Is_Not_Found_By_An_App_Admin(string targetRole)
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns([targetRole]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().GetItemAsync(GetQuery(isGlobalAdmin: false), TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData("testapp:admin")]
    [InlineData("global:admin")]
    public async Task An_Admin_Account_Is_Found_By_A_Global_Admin(string targetRole)
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns([targetRole]);

        // Act
        var vm = await GetHandler().GetItemAsync(GetQuery(isGlobalAdmin: true), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(_userDto, vm.User);
    }

    private GetUserByEmailQuery GetQuery(bool isGlobalAdmin) =>
        new()
        {
            Email = Email,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger("admin-id", isGlobalAdmin),
        };

    private GetUserByEmailQueryHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity", GlobalAdminRole = "global:admin" }),
            _userManager,
            _userRepository,
            _userRegisteredAppRepository
        );
}
