using Microsoft.AspNetCore.Identity;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.Roles;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.Roles;

public class RoleRepositoryTests
{
    [Fact]
    public async Task GetRoleDtosAsync_With_A_Prefix_Returns_The_Apps_Roles_Ordered_By_Name()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        AddRoles(context, "testapp:user", "otherapp:admin", "testapp:admin", "testapp2:admin");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var roles = await new RoleRepository(context).GetRoleDtosAsync("testapp:", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["testapp:admin", "testapp:user"], roles.Select(role => role.Name));
    }

    [Fact]
    public async Task GetRoleDtosAsync_Without_A_Prefix_Returns_Every_Role()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        AddRoles(context, "testapp:user", "global:admin", "otherapp:admin");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var roles = await new RoleRepository(context).GetRoleDtosAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["global:admin", "otherapp:admin", "testapp:user"], roles.Select(role => role.Name));
    }

    [Fact]
    public async Task GetUserRoleScopesAsync_Returns_Each_Of_The_Users_Roles_With_Its_Scopes()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var roles = AddRoles(context, "testapp:user", "testapp:owner", "otherapp:user", "testapp:viewer");
        context.UserRoles.AddRange(
            new IdentityUserRole<string> { UserId = "user-id", RoleId = roles[1].Id },
            new IdentityUserRole<string> { UserId = "user-id", RoleId = roles[0].Id },
            new IdentityUserRole<string> { UserId = "user-id", RoleId = roles[3].Id },
            new IdentityUserRole<string> { UserId = "other-user-id", RoleId = roles[2].Id }
        );
        context.RoleClaims.AddRange(
            new IdentityRoleClaim<string>
            {
                RoleId = roles[0].Id,
                ClaimType = "scope",
                ClaimValue = "useridentity-user:read",
            },
            new IdentityRoleClaim<string>
            {
                RoleId = roles[1].Id,
                ClaimType = "scope",
                ClaimValue = "useridentity-user:read",
            },
            new IdentityRoleClaim<string>
            {
                RoleId = roles[1].Id,
                ClaimType = "scope",
                ClaimValue = "catalog-product:all",
            },
            new IdentityRoleClaim<string>
            {
                RoleId = roles[2].Id,
                ClaimType = "scope",
                ClaimValue = "files-file:create",
            }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var roleScopes = await new RoleRepository(context).GetUserRoleScopesAsync("user-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["testapp:owner", "testapp:user", "testapp:viewer"], roleScopes.Keys.Order());
        Assert.Equal(["catalog-product:all", "useridentity-user:read"], roleScopes["testapp:owner"].Order());
        Assert.Equal(["useridentity-user:read"], roleScopes["testapp:user"]);
        Assert.Empty(roleScopes["testapp:viewer"]);
    }

    [Fact]
    public async Task GetUserRoleScopesAsync_For_A_User_Without_Roles_Returns_No_Roles()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();

        // Act
        var roleScopes = await new RoleRepository(context).GetUserRoleScopesAsync("user-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(roleScopes);
    }

    private static IdentityRole[] AddRoles(AppDbContext context, params string[] names)
    {
        var roles = names
            .Select(name => new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
            })
            .ToArray();

        context.Roles.AddRange(roles);

        return roles;
    }
}
