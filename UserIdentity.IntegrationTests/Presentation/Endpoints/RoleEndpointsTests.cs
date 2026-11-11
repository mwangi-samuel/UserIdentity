using System.Net;
using Newtonsoft.Json.Linq;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Domain.RoleClaims;
using UserIdentity.Domain.Roles;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class RoleEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private static readonly string _baseUri = "/api/v1/role";

    [Fact]
    public async Task Get_Roles_Returns_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);
        TestDbHelper.CreateIdentityRole("otherapp:admin");

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Items fetched successfully", $"{jsonObject["statusMessage"]}");

        var roles = jsonObject["roles"]?.ToObject<List<RoleDto>>();

        Assert.NotNull(roles);
        Assert.Equal([additionalRolename, TestDbHelper.AdminRoles.First(), TestDbHelper.DefaultRole], roles.Select(r => r.Name));
        Assert.Equal(role.Id, roles[0].Id);
    }

    [Fact]
    public async Task Get_Roles_For_A_Global_Admin_Includes_Other_Apps_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();
        TestDbHelper.CreateIdentityRole("otherapp:admin");

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var roles = SerDeHelper.Deserialize<JObject>(responseString)?["roles"]?.ToObject<List<RoleDto>>();

        Assert.NotNull(roles);
        Assert.Contains(roles, r => r.Name == "otherapp:admin");
        Assert.Contains(roles, r => r.Name == TestDbHelper.DefaultRole);
    }

    [Fact]
    public async Task Get_Roles_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        _ = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Roles_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        _ = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Roles_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_Returns_Role_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);
        var additionalRoleId = role.Id;

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + additionalRoleId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

        var responseRole = jsonObject["role"]?.ToObject<RoleDto>();

        Assert.NotNull(responseRole);
        Assert.Equal(additionalRoleId, responseRole.Id);
        Assert.Equal(additionalRolename, responseRole.Name);
    }

    [Fact]
    public async Task Get_Role_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Valid_Role_Returns_Role_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(additionalRolename);

        (_, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestPayload = new { RoleName = "testapp:newrole" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var role = jsonObject["role"]?.ToObject<RoleDto>();

        Assert.NotNull(role);
        Assert.NotNull(role.Id);
        Assert.Equal(requestPayload.RoleName, role.Name);
    }

    [Fact]
    public async Task Create_Existing_Role_Does_Not_Create_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleName = additionalRolename };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"A record identified with - {requestPayload.RoleName} - exists", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Create_Role_With_Invalid_Payload_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";

        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(1, errorList?.Count);

        Assert.True(errorList?.Any(x => x.Field == "RoleName" && x.Message == "The RoleName field is required.") ?? false);
    }

    [Fact]
    public async Task Create_Role_With_Name_Over_Its_Limit_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var requestPayload = new { RoleName = "testapp:" + new string('r', RoleLimits.NameMaxLength) };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleName = "testapp:newrole" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleName = "testapp:newrole" };

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        var requestPayload = new { RoleName = "testapp:newrole" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_Existing_Role_Updates_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var newRole = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleId = newRole.Id, RoleName = "testapp:updatednewrole" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + requestPayload.RoleId, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item updated successfully", $"{jsonObject["statusMessage"]}");

        var role = jsonObject["role"]?.ToObject<RoleDto>();

        Assert.NotNull(role);
        Assert.Equal(requestPayload.RoleId, role.Id);
        Assert.Equal(requestPayload.RoleName, role.Name);
    }

    [Fact]
    public async Task Update_Role_Renames_It_On_The_Unused_Invites_That_Grant_It()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        TestDbHelper.DeleteIdnetityRole("testapp:seller");

        var role = TestDbHelper.CreateIdentityRole("testapp:owner");
        var unusedInvite = TestDbHelper.CreateInviteCodeEntity("invited@example.com", RegisteredApp.AppName, role: role.Name);
        var usedInvite = TestDbHelper.CreateInviteCodeEntity("joined@example.com", RegisteredApp.AppName, applied: true, role: role.Name);

        var requestPayload = new { RoleName = "testapp:seller" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + role.Id, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("testapp:seller", TestDbHelper.GetInviteCodeEntityWithNoTracking(unusedInvite.Id)?.Role);
        Assert.Equal("testapp:owner", TestDbHelper.GetInviteCodeEntityWithNoTracking(usedInvite.Id)?.Role);
    }

    [Fact]
    public async Task Update_Non_Existing_Role_Does_Not_Update_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleId = Guid.NewGuid().ToString(), RoleName = additionalRolename };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + requestPayload.RoleId, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {requestPayload.RoleId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Update_Role_With_Invalid_Payload_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + role.Id, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(1, errorList?.Count);
        Assert.True(errorList?.Any(x => x.Field == "RoleName" && x.Message == "The RoleName field is required.") ?? false);
    }

    [Fact]
    public async Task Update_Role_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleName = "testapp:updatednewrole" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + role.Id, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_Role_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { RoleName = "testapp:updatedrole" };

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + role.Id, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_Role_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        var requestPayload = new { RoleName = "testapp:updatedrole" };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Put, _baseUri + "/" + role.Id, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_Returns_Role_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + role.Id);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item deleted successfully", $"{jsonObject["statusMessage"]}");
    }

    [Fact]
    public async Task Delete_Role_Granted_By_An_Unused_Invite_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var role = TestDbHelper.CreateIdentityRole("testapp:owner");
        TestDbHelper.CreateInviteCodeEntity("invited@example.com", RegisteredApp.AppName, role: role.Name);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + role.Id);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("A role that unused invite codes grant can't be deleted", $"{jsonObject["error"]?["message"]}");
        Assert.NotNull(TestDbHelper.GetIdentityRole("testapp:owner"));
    }

    [Fact]
    public async Task Delete_Non_Existent_Role_Does_Not_Delete_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var nonExistentRuleId = Guid.NewGuid().ToString();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + nonExistentRuleId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {nonExistentRuleId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Delete_Role_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_User_Roles_Returns_User_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/user/" + UserSettingHelper.UserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Items fetched successfully", $"{jsonObject["statusMessage"]}");

        var roles = jsonObject["userRoles"]?.ToObject<List<string>>();

        Assert.NotNull(roles);
        Assert.Equal(1, roles?.Count);

        Assert.Contains(TestDbHelper.DefaultRole, roles?[0]);
    }

    [Fact]
    public async Task Get_User_Non_Existent_User_Id_Does_Not_Delete_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var nonExistentUserId = Guid.NewGuid().ToString();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/user/" + nonExistentUserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {nonExistentUserId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Get_User_Roles_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, _baseUri + "/user/" + UserSettingHelper.UserId);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_User_Roles_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Get, _baseUri + "/user/" + UserSettingHelper.UserId);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_User_Roles_For_User_Returns_User_Roles()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/user/" + UserSettingHelper.UserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Items fetched successfully", $"{jsonObject["statusMessage"]}");

        var roles = jsonObject["userRoles"]?.ToObject<List<string>>();

        Assert.NotNull(roles);
        Assert.Equal(1, roles?.Count);

        Assert.Contains(roleName, roles?[0]);
    }

    [Fact]
    public async Task Create_Valid_User_Role_Returns_Role_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { UserSettingHelper.UserId, RoleId = role.Id };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var roles = jsonObject["userRoles"]?.ToObject<List<string>>();

        Assert.NotNull(roles);
        Assert.Equal(3, roles?.Count);

        Assert.Contains(roles!, r => r == TestDbHelper.DefaultRole);
        Assert.Contains(roles!, r => r == TestDbHelper.AdminRoles.First());
        Assert.Contains(roles!, r => r == additionalRolename);
    }

    [Fact]
    public async Task Create_Existing_User_Role_Does_Not_Create_Role()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);
        TestDbHelper.CreateIdentityUserRole(additionalRolename, UserSettingHelper.UserId);

        var requestPayload = new { UserSettingHelper.UserId, RoleId = role.Id };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"A record identified with - {requestPayload.RoleId} - exists", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Create_User_Role_With_Invalid_Payload_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var additionalRolename = "testapp:additionalrole";

        TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);
        await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(2, errorList?.Count);

        Assert.True(errorList?.Any(x => x.Field == "UserId" && x.Message == "The UserId field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "RoleId" && x.Message == "The RoleId field is required.") ?? false);
    }

    [Fact]
    public async Task Create_User_Role_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { UserSettingHelper.UserId, RoleId = role.Id };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_User_Role_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { UserSettingHelper.UserId, RoleId = role.Id };

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_User_Role_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        var requestPayload = new { UserSettingHelper.UserId, RoleId = role.Id };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/user", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_Valid_Role_Claim_Returns_Role_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var roleClaim = jsonObject["roleClaim"]?.ToObject<RoleClaimDto>();

        Assert.NotNull(roleClaim);
        Assert.Equal(requestPayload.Resource, roleClaim.Resource);
        Assert.Equal(requestPayload.Action, roleClaim.Action);
        Assert.Equal($"{requestPayload.Resource}{Separators.ServiceRole}{requestPayload.Action}", roleClaim.Scope);
    }

    [Fact]
    public async Task Create_Existing_User_Role_Claim_Does_Not_Create_Role_Claim()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var createRoleClaimResult = TestDbHelper.UpdateRoleClaim(additionalRolename, $"user{Separators.ScopeClaim}edit");

        Assert.True(createRoleClaimResult);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "edit",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"A record identified with - {$"user{Separators.ScopeClaim}edit"} - exists", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Create_Role_Claim_With_Resource_Over_Its_Limit_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var role = TestDbHelper.CreateIdentityRole("testapp:additionalrole");

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = new string('r', RoleClaimLimits.ResourceMaxLength + 1),
            Action = "read",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_Claim_With_Invalid_Payload_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRoleId = Guid.NewGuid().ToString();
        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(3, errorList?.Count);

        Assert.True(errorList?.Any(x => x.Field == "RoleId" && x.Message == "The RoleId field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "Resource" && x.Message == "The Resource field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "Action" && x.Message == "The Action field is required.") ?? false);
    }

    [Fact]
    public async Task Create_Role_Claim_With_Unknown_Action_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "write",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorList = SerDeHelper.Deserialize<JObject>(responseString)?["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.True(errorList?.Any(x => x.Field == "Action") ?? false);
        Assert.Empty(TestDbHelper.GetRoleClaims(additionalRolename));
    }

    [Fact]
    public async Task Create_Role_Claim_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_Claim_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Role_Claim_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_Claims_Returns_Role_Claims()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);
        var createRoleClaimResult = TestDbHelper.UpdateRoleClaim(additionalRolename, $"user{Separators.ScopeClaim}edit");

        Assert.True(createRoleClaimResult);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/claim/" + role.Id);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Items fetched successfully", $"{jsonObject["statusMessage"]}");

        var roleClaims = jsonObject["roleClaims"]?.ToObject<List<RoleClaimDto>>();

        Assert.NotNull(roleClaims);
        Assert.Equal(1, roleClaims?.Count);
        Assert.Equal("user", roleClaims?[0].Resource);
        Assert.Equal("edit", roleClaims?[0].Action);
        Assert.Equal($"user{Separators.ScopeClaim}edit", roleClaims?[0].Scope);
    }

    [Fact]
    public async Task Get_Non_Existent_Role_Claim_Does_Not_Return_Role_Claim()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var nonExistentRoleId = Guid.NewGuid().ToString();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/claim/" + nonExistentRoleId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {nonExistentRoleId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Get_Role_Claims_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, _baseUri + "/claim/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_Claims_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Get, _baseUri + "/claim/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Role_Claims_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/claim/" + role.Id);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_User_Role_Claim_Deletes_Role_Claim()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var createRoleClaimResult = TestDbHelper.UpdateRoleClaim(additionalRolename, $"user{Separators.ScopeClaim}edit");

        Assert.True(createRoleClaimResult);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "edit",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item deleted successfully", $"{jsonObject["statusMessage"]}");
    }

    [Fact]
    public async Task Delete_Non_Existing_Role_Claim_Does_Not_Delete_Role_Claim()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";

        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal(
            $"No record exists for the provided identifier - {requestPayload.Resource}:{requestPayload.Action}",
            $"{jsonObject["error"]?["message"]}"
        );

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Delete_Role_Claim_With_Invalid_Payload_Returns_Validation_Result()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Equal(3, errorList?.Count);

        Assert.True(errorList?.Any(x => x.Field == "RoleId" && x.Message == "The RoleId field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "Resource" && x.Message == "The Resource field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "Action" && x.Message == "The Action field is required.") ?? false);
    }

    [Fact]
    public async Task Delete_Role_Claim_With_No_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_Claim_With_Invalid_Auth_Returns_Auth_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var additionalRolename = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(additionalRolename);

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendInvalidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Role_Claim_With_Invalid_Permissions_Returns_Auth_Error()
    {
        // Arrange
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        var roleName = "testapp:additionalrole";
        var role = TestDbHelper.CreateIdentityRole(roleName);
        TestDbHelper.CreateIdentityUserRole(roleName, user.Id);
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateRefreshTokenEntity();

        var requestPayload = new
        {
            RoleId = role.Id,
            Resource = "user",
            Action = "create",
        };

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, _baseUri + "/claim", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
