using Microsoft.AspNetCore.Identity;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Configurations;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class PasswordResetConfigurationTests
{
    [Fact]
    public void Configure_PasswordReset_Should_Configure_PasswordReset()
    {
        // Arrange
        var passwordResetConfiguration = new PasswordResetConfiguration();
        (var builder, var entityType) = EntityConfigurationTestsHelper<PasswordResetEntity>.GetEntityTypeBuilder();

        // Act
        passwordResetConfiguration.Configure(builder);

        // Assert
        Assert.True(entityType.ConfirmColumnHasIndex(nameof(PasswordResetEntity.RequestId)));
        Assert.True(entityType.ConfirmCascadingForeignKey<IdentityUser>(nameof(PasswordResetEntity.UserId)));
    }
}
