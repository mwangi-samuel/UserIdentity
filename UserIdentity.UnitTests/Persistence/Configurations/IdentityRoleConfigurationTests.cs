using Microsoft.AspNetCore.Identity;
using UserIdentity.Domain.Roles;
using UserIdentity.Persistence.Configurations;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class IdentityRoleConfigurationTests
{
    [Fact]
    public void Configure_Should_Limit_The_Role_Name()
    {
        // Arrange
        var configuration = new IdentityRoleConfiguration();
        (var builder, var entityType) = EntityConfigurationTestsHelper<IdentityRole>.GetEntityTypeBuilder();

        // Act
        configuration.Configure(builder);

        // Assert
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityRole.Name), RoleLimits.NameMaxLength));
        Assert.True(entityType.ConfirmMaxColumnLength(nameof(IdentityRole.NormalizedName), RoleLimits.NameMaxLength));
    }
}
