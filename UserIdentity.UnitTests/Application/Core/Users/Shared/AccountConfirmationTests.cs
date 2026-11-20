using Microsoft.AspNetCore.Identity;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.AppSignInSettings;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class AccountConfirmationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void HasConfirmedContact_Is_True_When_The_Email_Or_The_Phone_Number_Is_Confirmed(
        bool emailConfirmed,
        bool phoneNumberConfirmed,
        bool expected
    )
    {
        // Arrange
        var user = new IdentityUser { EmailConfirmed = emailConfirmed, PhoneNumberConfirmed = phoneNumberConfirmed };

        // Act & Assert
        Assert.Equal(expected, AccountConfirmation.HasConfirmedContact(user));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void IsConfirmed_Needs_A_Confirmed_Contact_Only_When_The_App_Requires_It(
        bool requireConfirmedAccount,
        bool emailConfirmed,
        bool expected
    )
    {
        // Arrange
        var signInSettings = new AppSignInSettingsDto { AppId = 1, RequireConfirmedAccount = requireConfirmedAccount };
        var user = new IdentityUser { EmailConfirmed = emailConfirmed };

        // Act & Assert
        Assert.Equal(expected, AccountConfirmation.IsConfirmed(signInSettings, user));
    }
}
