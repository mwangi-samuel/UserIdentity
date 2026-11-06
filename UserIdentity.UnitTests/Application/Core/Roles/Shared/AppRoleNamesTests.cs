using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Roles.Shared;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Shared;

public class AppRoleNamesTests
{
    [Theory]
    [InlineData("testapp:admin", "TestApp", true)]
    [InlineData("TestApp:Editor", "testapp", true)]
    [InlineData("testapp2:admin", "TestApp", false)]
    [InlineData("otherapp:admin", "TestApp", false)]
    [InlineData("testapp", "TestApp", false)]
    [InlineData(null, "TestApp", false)]
    public void IsAppRole_Matches_Only_The_Apps_Prefix(string? roleName, string appName, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, AppRoleNames.IsAppRole(roleName, appName));
    }

    [Theory]
    [InlineData("testapp:admin", "TestApp", "superuser", true)]
    [InlineData("TestApp:SuperUser", "TestApp", "admin, superuser", true)]
    [InlineData("testapp:owner", "TestApp", "admin,superuser", false)]
    [InlineData("otherapp:admin", "TestApp", "admin", false)]
    [InlineData(" testapp:admin ", "TestApp", "superuser", true)]
    [InlineData(null, "TestApp", "admin", false)]
    public void IsAppAdminRole_Matches_The_Apps_Admin_Roles_And_Its_Admin_Role(
        string? roleName,
        string appName,
        string adminRoles,
        bool expected
    )
    {
        // Arrange
        var roleSettings = new RoleSettings { ServiceName = "UserIdentity", AdminRoles = adminRoles };

        // Act & Assert
        Assert.Equal(expected, AppRoleNames.IsAppAdminRole(roleName, appName, roleSettings));
    }

    [Fact]
    public void Prefix_Is_The_Lower_Case_App_Name_And_The_Separator()
    {
        // Act & Assert
        Assert.Equal("testapp:", AppRoleNames.Prefix(" TestApp "));
    }

    [Fact]
    public void DefaultRole_Is_The_Apps_Lower_Case_Default_Role()
    {
        // Arrange
        var roleSettings = new RoleSettings { ServiceName = "UserIdentity", DefaultRole = " Shopper " };

        // Act & Assert
        Assert.Equal("testapp:shopper", AppRoleNames.DefaultRole("TestApp", roleSettings));
    }

    [Fact]
    public void ForApp_Keeps_The_Apps_Roles_And_The_Global_Admin_Role()
    {
        // Act
        var roles = AppRoleNames.ForApp(
            ["testapp:user", "otherapp:admin", "global:admin", "Administrator", "service:internal"],
            "TestApp",
            "global:admin"
        );

        // Assert
        Assert.Equal(["testapp:user", "global:admin"], roles);
    }

    [Fact]
    public void ForApp_Drops_The_Global_Admin_Role_When_None_Is_Configured()
    {
        // Act
        var roles = AppRoleNames.ForApp(["testapp:user", "global:admin"], "TestApp", "");

        // Assert
        Assert.Equal(["testapp:user"], roles);
    }
}
