using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class UserChangeAccessTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly RoleSettings _roleSettings = new() { ServiceName = "useridentity", GlobalAdminRole = "global:admin" };
    private readonly IdentityUser _target = new() { Id = "target-id" };

    public UserChangeAccessTests()
    {
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_target.Id, "TestApp", A<CancellationToken>._))
            .Returns(true);
        A.CallTo(() => _userManager.GetRolesAsync(_target)).Returns(["testapp:user"]);
    }

    [Theory]
    [InlineData("testapp:admin")]
    [InlineData("global:admin")]
    public async Task An_App_Admin_Cannot_Change_An_Admin_Account(string targetRole)
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_target)).Returns([targetRole]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() => EnsureAsync(_target, new UserChanger("admin-id", false)));
    }

    [Fact]
    public async Task A_Global_Admin_Can_Change_An_Admin_Account()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_target)).Returns(["testapp:admin"]);

        // Act
        var user = await EnsureAsync(_target, new UserChanger("global-admin-id", true));

        // Assert
        Assert.Same(_target, user);
    }

    [Fact]
    public async Task An_Admin_Can_Change_Their_Own_Account()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_target)).Returns(["testapp:admin"]);

        // Act
        var user = await EnsureAsync(_target, new UserChanger(_target.Id, false));

        // Assert
        Assert.Same(_target, user);
    }

    [Fact]
    public async Task An_App_Admin_Can_Change_Another_Account_Of_The_App()
    {
        // Act
        var user = await EnsureAsync(_target, new UserChanger("admin-id", false));

        // Assert
        Assert.Same(_target, user);
    }

    [Fact]
    public async Task An_Account_Of_Another_App_Is_Not_Found()
    {
        // Arrange
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_target.Id, "TestApp", A<CancellationToken>._))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() => EnsureAsync(_target, new UserChanger("global-admin-id", true)));
    }

    [Fact]
    public async Task An_Unknown_Account_Is_Not_Found() =>
        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() => EnsureAsync(null, new UserChanger("admin-id", false)));

    private Task<IdentityUser> EnsureAsync(IdentityUser? user, UserChanger changer) =>
        UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            user,
            "target-id",
            "TestApp",
            changer,
            TestContext.Current.CancellationToken
        );
}
