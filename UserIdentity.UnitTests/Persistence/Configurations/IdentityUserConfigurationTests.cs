using Microsoft.AspNetCore.Identity;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Configurations;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class IdentityUserConfigurationTests
{
    [Fact]
    public void Configure_Should_Limit_The_Columns_Sign_Up_Fills()
    {
        // Arrange
        var configuration = new IdentityUserConfiguration();
        (var builder, var entityType) = EntityConfigurationTestsHelper<IdentityUser>.GetEntityTypeBuilder();

        // Act
        configuration.Configure(builder);

        // Assert
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityUser.UserName), UserLimits.UserNameMaxLength));
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityUser.NormalizedUserName), UserLimits.UserNameMaxLength));
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityUser.Email), UserLimits.EmailMaxLength));
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityUser.NormalizedEmail), UserLimits.EmailMaxLength));
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityUser.PhoneNumber), UserLimits.PhoneNumberMaxLength));
    }
}
