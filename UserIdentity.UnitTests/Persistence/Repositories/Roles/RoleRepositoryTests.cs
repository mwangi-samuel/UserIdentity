using System;
using System.Linq;
using System.Threading.Tasks;

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
  public async Task GetUserRoleNamesAsync_Returns_Only_The_Users_Roles()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var roles = AddRoles(context, "testapp:user", "testapp:owner", "otherapp:user");
    context.UserRoles.AddRange(
      new IdentityUserRole<string> { UserId = "user-id", RoleId = roles[1].Id },
      new IdentityUserRole<string> { UserId = "user-id", RoleId = roles[0].Id },
      new IdentityUserRole<string> { UserId = "other-user-id", RoleId = roles[2].Id });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    var roleNames = await new RoleRepository(context).GetUserRoleNamesAsync("user-id", TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["testapp:owner", "testapp:user"], roleNames);
  }

  [Fact]
  public async Task GetScopesAsync_Returns_The_Distinct_Scopes_Of_The_Named_Roles()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var roles = AddRoles(context, "testapp:user", "testapp:owner", "otherapp:user");
    context.RoleClaims.AddRange(
      new IdentityRoleClaim<string> { RoleId = roles[0].Id, ClaimType = "scope", ClaimValue = "useridentity-user:read" },
      new IdentityRoleClaim<string> { RoleId = roles[1].Id, ClaimType = "scope", ClaimValue = "useridentity-user:read" },
      new IdentityRoleClaim<string> { RoleId = roles[1].Id, ClaimType = "scope", ClaimValue = "catalog-product:all" },
      new IdentityRoleClaim<string> { RoleId = roles[2].Id, ClaimType = "scope", ClaimValue = "files-file:create" });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    var scopes = await new RoleRepository(context).GetScopesAsync(["TestApp:User", "testapp:owner"], TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["catalog-product:all", "useridentity-user:read"], scopes.Order());
  }

  [Fact]
  public async Task GetScopesAsync_Without_Roles_Returns_No_Scopes()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();

    // Act
    var scopes = await new RoleRepository(context).GetScopesAsync([], TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(scopes);
  }

  private static IdentityRole[] AddRoles(AppDbContext context, params string[] names)
  {
    var roles = names.Select(name => new IdentityRole { Id = Guid.NewGuid().ToString(), Name = name, NormalizedName = name.ToUpperInvariant() }).ToArray();

    context.Roles.AddRange(roles);

    return roles;
  }
}
