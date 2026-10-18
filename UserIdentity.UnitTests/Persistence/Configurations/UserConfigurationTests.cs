using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Configurations;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Configurations;

public class UserConfigurationTests
{
  [Fact]
  public void Configure_User_Should_Configure_User()
  {
    // Arrange
    var userConfiguration = new UserConfiguration();
    (var builder, var entityType) = EntityConfigurationTestsHelper<UserEntity>.GetEntityTypeBuilder();

    // Act
    userConfiguration.Configure(builder);

    // Assert
    Assert.True(entityType.ConfirmMaxColumnLength(nameof(UserEntity.EmailConfirmationToken), 600));
    Assert.True(entityType.ConfirmMaxColumnLength(nameof(UserEntity.FirstName), 20));
    Assert.True(entityType.ConfirmMaxColumnLength(nameof(UserEntity.LastName), 20));

    Assert.True(entityType.ConfirmColumnHasKey(nameof(UserEntity.Id)));
  }
}
