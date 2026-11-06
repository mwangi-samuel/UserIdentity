using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Configurations;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class InviteCodeConfigurationTests
{
    [Fact]
    public void Configure_InviteCode_Should_Configure_InviteCode()
    {
        // Arrange
        var inviteCodeConfiguration = new InviteCodeConfiguration();
        (var builder, var entityType) = EntityConfigurationTestsHelper<InviteCodeEntity>.GetEntityTypeBuilder();

        // Act
        inviteCodeConfiguration.Configure(builder);

        // Assert
        Assert.True(entityType.ConfirmColumnHasKey(nameof(InviteCodeEntity.Id)));
    }
}
