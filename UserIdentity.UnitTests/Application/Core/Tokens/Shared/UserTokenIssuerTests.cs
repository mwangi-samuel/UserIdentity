using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;

using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Shared;

public class UserTokenIssuerTests
{
  private static readonly DateTime _now = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

  private readonly IRoleRepository _roleRepository = A.Fake<IRoleRepository>();
  private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
  private readonly IJwtTokenHandler _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
  private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
  private readonly IMachineDateTime _machineDateTime = A.Fake<IMachineDateTime>();

  public UserTokenIssuerTests()
  {
    A.CallTo(() => _machineDateTime.Now).Returns(_now);
    A.CallTo(() => _roleRepository.GetUserRoleNamesAsync("user-id", A<CancellationToken>._))
      .Returns(["global:admin", "otherapp:admin", "service:internal", "testapp:owner", "testapp:user"]);
  }

  [Fact]
  public async Task CreateAccessTokenAsync_Keeps_The_Apps_Roles_And_The_Global_Admin_Role_With_Their_Scopes()
  {
    // Arrange
    HashSet<string> scopes = ["useridentity-user:read", "catalog-product:all"];

    A.CallTo(() => _roleRepository.GetScopesAsync(A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { "global:admin", "testapp:owner", "testapp:user" }), A<CancellationToken>._))
      .Returns(scopes);
    A.CallTo(() => _jwtTokenHandler.CreateToken("user-id", "test.user", "TestApp",
        A<HashSet<string>>.That.Matches(roles => roles.SetEquals(new[] { "global:admin", "testapp:owner", "testapp:user" })), scopes, A<HashSet<Claim>?>._))
      .Returns(("access-token", 180));

    var issuer = CreateIssuer();

    // Act
    var accessToken = await issuer.CreateAccessTokenAsync("user-id", "test.user", "TestApp", TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("access-token", accessToken.Token);
    Assert.Equal(180, accessToken.ExpiresIn);
  }

  [Fact]
  public async Task StartSessionAsync_Adds_A_Hashed_Refresh_Token_That_Expires_After_The_Configured_Time()
  {
    // Arrange
    A.CallTo(() => _jwtTokenHandler.CreateToken(A<string>._, A<string>._, A<string>._, A<HashSet<string>>._, A<HashSet<string>>._, A<HashSet<Claim>?>._))
      .Returns(("access-token", 180));
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns("refresh-token");
    A.CallTo(() => _tokenFactory.HashToken("refresh-token")).Returns("refresh-token-hash");

    RefreshTokenEntity? stored = null;
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).Invokes((RefreshTokenEntity entity) => stored = entity);

    var issuer = CreateIssuer();

    // Act
    var session = await issuer.StartSessionAsync("user-id", "test.user", "TestApp", TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("access-token", session.AccessToken?.Token);
    Assert.Equal("refresh-token", session.RefreshToken);

    Assert.NotNull(stored);
    Assert.Equal("user-id", stored.UserId);
    Assert.Equal("refresh-token-hash", stored.Token);
    Assert.Equal(_now.AddSeconds(3600), stored.Expires);
    Assert.NotEqual(Guid.Empty, stored.Version);
    Assert.Equal("user-id", stored.CreatedBy);
    Assert.Equal(_now, stored.CreatedAt);
  }

  private UserTokenIssuer CreateIssuer() => new(
    _roleRepository,
    _refreshTokenRepository,
    _jwtTokenHandler,
    _tokenFactory,
    _machineDateTime,
    new RoleSettings { ServiceName = "UserIdentity" },
    new RefreshTokenSettings { ValidForSeconds = 3600 });
}
