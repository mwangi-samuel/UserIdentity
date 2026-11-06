using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Configurations;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class WaitListConfigurationTests
{
    [Fact]
    public void Configure_WaitList_Should_Configure_WaitList()
    {
        // Arrange
        var waitListConfiguration = new WaitListConfiguration();
        (var builder, var entityType) = EntityConfigurationTestsHelper<WaitListEntity>.GetEntityTypeBuilder();

        // Act
        waitListConfiguration.Configure(builder);

        // Assert
        Assert.True(entityType.ConfirmColumnHasKey(nameof(WaitListEntity.Id)));
        Assert.True(entityType.ConfirmColumnHasIndex(nameof(WaitListEntity.UserEmail)));
    }
}
