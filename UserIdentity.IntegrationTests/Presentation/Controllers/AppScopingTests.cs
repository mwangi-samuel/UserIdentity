using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.IdentityModel.JsonWebTokens;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Security.Jwt;

using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Controllers;

public class AppScopingTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private const string OtherAppRole = "otherapp:admin";
  private const string OwnerRole = "testapp:owner";

  [Fact]
  public async Task Login_To_An_App_The_User_Isnt_Registered_For_Returns_Unauthorized()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiRequestHelper.loginUrl)
    {
      Content = SerDeHelper.ConvertToHttpContent(new { UserName = UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword })
    };
    httpRequest.AddXApiKey();
    httpRequest.Headers.Add("X-App-Name", TestingWebAppFactory.OtherAppName);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Login_Token_Carries_Only_The_Apps_Roles()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(OtherAppRole);
    _testDbHelper.CreateIdentityUserRole(OtherAppRole, UserSettingHelper.UserId);

    // Act
    (var token, _) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    // Assert
    var roles = new JsonWebToken(token).Claims.Where(c => c.Type == JwtCustomClaimNames.Roles).Select(c => c.Value).ToList();

    Assert.Contains(_testDbHelper.DefaultRole, roles);
    Assert.DoesNotContain(OtherAppRole, roles);
  }

  [Fact]
  public async Task Login_Token_Carries_Only_The_Apps_Role_Scopes()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(OwnerRole);
    _testDbHelper.CreateIdentityUserRole(OwnerRole, UserSettingHelper.UserId);
    _testDbHelper.UpdateRoleClaim(OwnerRole, "filemanager-file:create");
    _testDbHelper.CreateIdentityRole(OtherAppRole);
    _testDbHelper.CreateIdentityUserRole(OtherAppRole, UserSettingHelper.UserId);
    _testDbHelper.UpdateRoleClaim(OtherAppRole, "commercemanager-product:all");

    // Act
    (var token, _) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    // Assert
    var scopes = new JsonWebToken(token).Claims.Where(c => c.Type == JwtCustomClaimNames.Scope).Select(c => c.Value).ToList();

    Assert.Equal("filemanager-file:create", Assert.Single(scopes));
  }

  [Fact]
  public async Task App_Admins_Add_Scopes_To_Their_Apps_Roles()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    var role = _testDbHelper.CreateIdentityRole(OwnerRole);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/role/claim", new { RoleId = role.Id, Resource = "FileManager-File", Action = "Create" });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("filemanager-file:create", Assert.Single(_testDbHelper.GetRoleClaims(OwnerRole)));
  }

  [Fact]
  public async Task Adding_A_Scope_To_Another_Apps_Role_As_An_App_Admin_Returns_Not_Found()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    var role = _testDbHelper.CreateIdentityRole(OtherAppRole);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/role/claim", new { RoleId = role.Id, Resource = "filemanager-file", Action = "create" });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Empty(_testDbHelper.GetRoleClaims(OtherAppRole));
  }

  [Fact]
  public async Task Another_Apps_Role_Scopes_Are_Not_Found_For_An_App_Admin()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    var role = _testDbHelper.CreateIdentityRole(OtherAppRole);
    _testDbHelper.UpdateRoleClaim(OtherAppRole, "filemanager-file:create");

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"/api/v1/role/claim/{role.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Adding_A_Scope_To_The_Global_Admin_Role_Returns_Bad_Request()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.ConfigureIdentityUserAsGlobalAdmin();
    var globalAdminRole = _testDbHelper.GetIdentityRole(ZenConstants.GLOBAL_ADMIN_ROLE)!;

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/role/claim", new { RoleId = globalAdminRole.Id, Resource = "filemanager-file", Action = "create" });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Empty(_testDbHelper.GetRoleClaims(ZenConstants.GLOBAL_ADMIN_ROLE));
  }

  [Fact]
  public async Task Registered_Apps_Are_Forbidden_To_App_Admins()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, "/api/v1/registeredapp");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Registered_Apps_Are_Listed_For_Global_Admins()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.ConfigureIdentityUserAsGlobalAdmin();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, "/api/v1/registeredapp");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task Creating_Another_Apps_Role_As_An_App_Admin_Returns_Bad_Request()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    // Roles aren't reset between tests, and other tests here create this one
    _testDbHelper.DeleteIdnetityRole(OtherAppRole);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/role", new { RoleName = OtherAppRole });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Null(_testDbHelper.GetIdentityRole(OtherAppRole));
  }

  [Fact]
  public async Task Another_Apps_Invite_Code_Is_Not_Found_For_An_App_Admin()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "other-app-invite@example.com";
    _testDbHelper.CreateInviteCodeEntity(testEmail, TestingWebAppFactory.OtherAppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"/api/v1/invitecode/email/{testEmail}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}
