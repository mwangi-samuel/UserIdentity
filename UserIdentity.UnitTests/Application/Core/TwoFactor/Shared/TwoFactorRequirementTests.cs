using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AppSignInSettings;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Shared;

public class TwoFactorRequirementTests
{
    private readonly RoleSettings _roleSettings = new() { ServiceName = "useridentity", GlobalAdminRole = "global:admin" };

    [Theory]
    [InlineData(false, false, "testapp:user", false)]
    [InlineData(false, false, "testapp:admin", false)]
    [InlineData(true, false, "testapp:user", true)]
    [InlineData(true, false, "testapp:admin", true)]
    [InlineData(false, true, "testapp:user", false)]
    [InlineData(false, true, "testapp:admin", true)]
    [InlineData(false, true, "global:admin", true)]
    [InlineData(false, true, "otherapp:admin", false)]
    public void IsRequired_Applies_The_Switch_For_Everyone_And_The_Switch_For_Admins(
        bool requireTwoFactor,
        bool requireTwoFactorForAdmins,
        string role,
        bool expected
    )
    {
        // Arrange
        var signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireTwoFactor = requireTwoFactor,
            RequireTwoFactorForAdmins = requireTwoFactorForAdmins,
            AllowAuthenticatorTwoFactor = true,
        };

        // Act & Assert
        Assert.Equal(expected, TwoFactorRequirement.IsRequired(signInSettings, [role], "TestApp", _roleSettings));
    }

    [Fact]
    public void AllowedMethods_Lists_The_Allowed_Ones_Authenticator_First()
    {
        // Arrange
        var signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            AllowSmsTwoFactor = true,
            AllowEmailTwoFactor = true,
            AllowAuthenticatorTwoFactor = true,
        };

        // Act & Assert
        Assert.Equal(
            [TwoFactorMethod.Authenticator, TwoFactorMethod.Email, TwoFactorMethod.Sms],
            AllowedTwoFactorMethods.For(signInSettings)
        );
    }

    [Theory]
    [InlineData(TwoFactorMethod.Email, TwoFactorMethod.Email)]
    [InlineData(TwoFactorMethod.Sms, null)]
    [InlineData(null, null)]
    public void InUse_Counts_A_Method_The_App_No_Longer_Allows_As_None(TwoFactorMethod? method, TwoFactorMethod? expected)
    {
        // Arrange
        var signInSettings = new AppSignInSettingsDto { AppId = 1, AllowEmailTwoFactor = true };

        // Act & Assert
        Assert.Equal(expected, AllowedTwoFactorMethods.InUse(signInSettings, method));
    }
}
