using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;
using PolyzenKit.Common.Utilities;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class AppScopingTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string OtherAppRole = "otherapp:admin";
    private const string OwnerRole = "testapp:owner";

    [Fact]
    public async Task Login_To_An_App_The_User_Isnt_Registered_For_Returns_Unauthorized()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiRequestHelper.loginUrl)
        {
            Content = SerDeHelper.ConvertToHttpContent(new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }),
        };
        httpRequest.AddXApiKey();
        httpRequest.Headers.Add("X-App-Name", TestingWebAppFactory.OtherAppName);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Token_Carries_Only_The_Apps_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(OtherAppRole);
        TestDbHelper.CreateIdentityUserRole(OtherAppRole, UserSettingHelper.UserId);

        // Act
        (var token, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        // Assert
        var roles = new JsonWebToken(token).Claims.Where(c => c.Type == JwtCustomClaimNames.Roles).Select(c => c.Value).ToList();

        Assert.Contains(TestDbHelper.DefaultRole, roles);
        Assert.DoesNotContain(OtherAppRole, roles);
    }

    [Fact]
    public async Task Login_Token_Carries_Only_The_Apps_Role_Scopes()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(OwnerRole);
        TestDbHelper.CreateIdentityUserRole(OwnerRole, UserSettingHelper.UserId);
        TestDbHelper.UpdateRoleClaim(OwnerRole, "media-file:create");
        TestDbHelper.CreateIdentityRole(OtherAppRole);
        TestDbHelper.CreateIdentityUserRole(OtherAppRole, UserSettingHelper.UserId);
        TestDbHelper.UpdateRoleClaim(OtherAppRole, "catalog-product:all");

        // Act
        (var token, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        // Assert
        var scopes = new JsonWebToken(token).Claims.Where(c => c.Type == JwtCustomClaimNames.Scope).Select(c => c.Value).ToList();

        Assert.Equal("media-file:create", Assert.Single(scopes));
    }

    [Fact]
    public async Task App_Admins_Add_Scopes_To_Their_Apps_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        var role = TestDbHelper.CreateIdentityRole(OwnerRole);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            "/api/v1/role/claim",
            new
            {
                RoleId = role.Id,
                Resource = "Media-File",
                Action = "Create",
            }
        );

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("media-file:create", Assert.Single(TestDbHelper.GetRoleClaims(OwnerRole)));
    }

    [Fact]
    public async Task Adding_A_Scope_To_Another_Apps_Role_As_An_App_Admin_Returns_Not_Found()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        var role = TestDbHelper.CreateIdentityRole(OtherAppRole);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            "/api/v1/role/claim",
            new
            {
                RoleId = role.Id,
                Resource = "media-file",
                Action = "create",
            }
        );

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(TestDbHelper.GetRoleClaims(OtherAppRole));
    }

    [Fact]
    public async Task Another_Apps_Role_Scopes_Are_Not_Found_For_An_App_Admin()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        var role = TestDbHelper.CreateIdentityRole(OtherAppRole);
        TestDbHelper.UpdateRoleClaim(OtherAppRole, "media-file:create");

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"/api/v1/role/claim/{role.Id}");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_A_Scope_To_The_Global_Admin_Role_Returns_Bad_Request()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();
        var globalAdminRole = TestDbHelper.GetIdentityRole(KnownRoles.GlobalAdmin)!;

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            "/api/v1/role/claim",
            new
            {
                RoleId = globalAdminRole.Id,
                Resource = "media-file",
                Action = "create",
            }
        );

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(TestDbHelper.GetRoleClaims(KnownRoles.GlobalAdmin));
    }

    [Fact]
    public async Task Registered_Apps_Are_Forbidden_To_App_Admins()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, "/api/v1/registered-app");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Registered_Apps_Are_Listed_For_Global_Admins()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, "/api/v1/registered-app");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_Another_Apps_Role_As_An_App_Admin_Returns_Bad_Request()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        // Roles aren't reset between tests, and other tests here create this one
        TestDbHelper.DeleteIdnetityRole(OtherAppRole);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, "/api/v1/role", new { RoleName = OtherAppRole });

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(TestDbHelper.GetIdentityRole(OtherAppRole));
    }

    [Fact]
    public async Task Another_Apps_Invite_Code_Is_Not_Found_For_An_App_Admin()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var testEmail = "other-app-invite@example.com";
        TestDbHelper.CreateInviteCodeEntity(testEmail, TestingWebAppFactory.OtherAppName);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"/api/v1/invite-code/email/{testEmail}");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
