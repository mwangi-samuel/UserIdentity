using System.Security.Claims;
using FakeItEasy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Persistence.Repositories.Roles;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class CreateAccessTokenCommandHandlerTests
{
    private static readonly string[] AppRoles = ["global:admin", "testapp:owner", "testapp:user"];

    private readonly IRoleRepository _roleRepository = A.Fake<IRoleRepository>();
    private readonly IJwtTokenHandler _jwtTokenHandler = A.Fake<IJwtTokenHandler>();

    [Fact]
    public async Task Create_Access_Token_Keeps_The_Apps_Roles_And_The_Global_Admin_Role_With_Their_Scopes()
    {
        // Arrange
        HashSet<string> scopes = ["useridentity-user:read", "catalog-product:all"];

        A.CallTo(() => _roleRepository.GetUserRoleScopesAsync("user-id", A<CancellationToken>._))
            .Returns(
                new Dictionary<string, HashSet<string>>
                {
                    ["global:admin"] = [],
                    ["otherapp:admin"] = ["otherapp-secret:all"],
                    ["service:internal"] = ["service-secret:all"],
                    ["testapp:owner"] = ["useridentity-user:read", "catalog-product:all"],
                    ["testapp:user"] = ["useridentity-user:read"],
                }
            );
        A.CallTo(() =>
                _jwtTokenHandler.CreateToken(
                    "user-id",
                    "test.user",
                    "TestApp",
                    A<HashSet<string>>.That.Matches(roles => roles.SetEquals(AppRoles)),
                    A<HashSet<string>>.That.Matches(tokenScopes => tokenScopes.SetEquals(scopes)),
                    A<HashSet<Claim>?>._
                )
            )
            .Returns(("access-token", 180));

        var handler = new CreateAccessTokenCommandHandler(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _roleRepository,
            _jwtTokenHandler
        );

        // Act
        var vm = await handler.CreateItemAsync(
            new CreateAccessTokenCommand("user-id", "test.user", "TestApp", Guid.CreateVersion7()),
            "user-id",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("access-token", vm.AccessToken.Token);
        Assert.Equal(180, vm.AccessToken.ExpiresIn);
    }

    [Fact]
    public async Task Create_Access_Token_Carries_The_Session_Id_In_The_Sid_Claim()
    {
        // Arrange
        var sessionId = Guid.CreateVersion7();
        HashSet<Claim>? additionalClaims = null;

        A.CallTo(() => _roleRepository.GetUserRoleScopesAsync("user-id", A<CancellationToken>._))
            .Returns(new Dictionary<string, HashSet<string>> { ["testapp:user"] = [] });
        A.CallTo(() =>
                _jwtTokenHandler.CreateToken(
                    A<string>._,
                    A<string>._,
                    A<string>._,
                    A<HashSet<string>>._,
                    A<HashSet<string>>._,
                    A<HashSet<Claim>?>._
                )
            )
            .Invokes(
                (string _, string _, string _, HashSet<string> _, HashSet<string> _, HashSet<Claim>? claims) => additionalClaims = claims
            )
            .Returns(("access-token", 180));

        var handler = new CreateAccessTokenCommandHandler(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _roleRepository,
            _jwtTokenHandler
        );

        // Act
        await handler.CreateItemAsync(
            new CreateAccessTokenCommand("user-id", "test.user", "TestApp", sessionId),
            "user-id",
            TestContext.Current.CancellationToken
        );

        // Assert
        var claim = Assert.Single(additionalClaims ?? []);
        Assert.Equal(JwtRegisteredClaimNames.Sid, claim.Type);
        Assert.Equal(sessionId.ToString(), claim.Value);
    }
}
