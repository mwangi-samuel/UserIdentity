using System;

using PolyzenKit.Domain.Entity;

using UserIdentity.Domain.RefreshTokens;

using Xunit;

namespace UserIdentity.UnitTests.Domain.Identity;

public class RefreshTokenTests
{
  [Fact]
  public void New_RefreshToken_is_a_Valid_RefreshToken_Instance()
  {
    // Arrange
    RefreshTokenEntity refreshToken = new() { Id = Guid.NewGuid() };

    // Act & Assert
    Assert.IsType<BaseEntity<Guid>>(refreshToken, exactMatch: false);
  }
}

