using System.Net;

using Newtonsoft.Json.Linq;

using PolyzenKit.Presentation.Validation;

using UserIdentity.Domain.InviteCodes;
using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class InviteCodeEndpointsTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly static string _baseUri = "/api/v1/invite-code";
  private const string OwnerRole = "testapp:owner";

  #region GetInviteCode Tests

  [Fact]
  public async Task Get_InviteCode_By_Id_Returns_InviteCode()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "invitecode-test@example.com";
    var inviteCode = TestDbHelper.CreateInviteCodeEntity(testEmail, RegisteredApp.AppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDto>();

    Assert.NotNull(inviteCodeDetails);
    Assert.Equal(inviteCode.Id, inviteCodeDetails.Id);
    Assert.Equal(testEmail, inviteCodeDetails.UserEmail);
    Assert.Equal(inviteCode.InviteCode, inviteCodeDetails.InviteCode);
  }

  [Fact]
  public async Task Get_InviteCode_By_Email_Returns_InviteCode()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "invitecode-email-test@example.com";
    var inviteCode = TestDbHelper.CreateInviteCodeEntity(testEmail, RegisteredApp.AppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/email/{testEmail}");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDto>();

    Assert.NotNull(inviteCodeDetails);
    Assert.Equal(inviteCode.Id, inviteCodeDetails.Id);
    Assert.Equal(testEmail, inviteCodeDetails.UserEmail);
  }

  [Fact]
  public async Task Get_InviteCode_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    TestDbHelper.SeedDatabase();

    var testEmail = "invitecode-forbidden@example.com";
    var inviteCode = TestDbHelper.CreateInviteCodeEntity(testEmail, RegisteredApp.AppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Get_InviteCode_With_No_Auth_Returns_Unauthorized()
  {
    // Arrange
    TestDbHelper.SeedDatabase();

    var testEmail = "invitecode-noauth@example.com";
    var inviteCode = TestDbHelper.CreateInviteCodeEntity(testEmail, RegisteredApp.AppName);

    // Act
    var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Get_Non_Existing_InviteCode_Returns_NotFound()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var nonExistingId = 99999L;

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{nonExistingId}");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");
  }

  #endregion

  #region CreateInviteCode Tests

  [Fact]
  public async Task Create_InviteCode_Creates_And_Returns_InviteCode()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = "TESTCODE01";
    var userEmail = "new-invitecode@example.com";

    var requestPayload = new { inviteCode, userEmail };

    // Clean up any existing record
    TestDbHelper.DeleteInviteCodeEntityByEmail(userEmail);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    OutputHelper.WriteLine(responseString);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDto>();

    Assert.NotNull(inviteCodeDetails);
    Assert.Equal(userEmail, inviteCodeDetails.UserEmail);
    Assert.Equal(inviteCode, inviteCodeDetails.InviteCode);
    Assert.True(inviteCodeDetails.Id > 0);

    // Verify in database
    var savedEntity = TestDbHelper.GetInviteCodeEntityByEmail(userEmail);
    Assert.NotNull(savedEntity);
    Assert.Equal(inviteCode, savedEntity.InviteCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    TestDbHelper.SeedDatabase();

    var requestPayload = new { InviteCode = "FORBCODE01", UserEmail = "forbidden-invitecode@example.com" };

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_No_Auth_Returns_Unauthorized()
  {
    // Arrange
    TestDbHelper.SeedDatabase();

    var requestPayload = new { InviteCode = "NOAUTHCODE", UserEmail = "noauth-invitecode@example.com" };

    // Act
    var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_Role_Returns_The_Role()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();
    TestDbHelper.CreateIdentityRole(OwnerRole);

    var userEmail = "owner-invitecode@example.com";

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(
      HttpMethod.Post,
      _baseUri,
      new { inviteCode = "OWNERCODE1", userEmail, role = OwnerRole });

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var inviteCodeDetails = SerDeHelper.Deserialize<JObject>(responseString)?["inviteCode"]?.ToObject<InviteCodeDto>();

    Assert.NotNull(inviteCodeDetails);
    Assert.Equal(OwnerRole, inviteCodeDetails.Role);
    Assert.False(inviteCodeDetails.Applied);
    Assert.Equal(OwnerRole, TestDbHelper.GetInviteCodeEntityByEmail(userEmail)?.Role);
  }

  [Theory]
  [InlineData("testapp:admin")]
  [InlineData("otherapp:owner")]
  [InlineData("testapp:missing")]
  public async Task Create_InviteCode_With_An_Admin_Another_Apps_Or_A_Missing_Role_Returns_BadRequest(string role)
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();
    TestDbHelper.CreateIdentityRole("otherapp:owner");

    var userEmail = "bad-role-invitecode@example.com";

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(
      HttpMethod.Post,
      _baseUri,
      new { inviteCode = "BADROLE001", userEmail, role });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Null(TestDbHelper.GetInviteCodeEntityByEmail(userEmail));
  }

  #endregion

  #region GetInviteCodes Tests

  [Fact]
  public async Task Get_InviteCodes_Returns_The_Apps_InviteCodes_Newest_First()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var now = DateTime.UtcNow;
    TestDbHelper.CreateInviteCodeEntity("first-list@example.com", RegisteredApp.AppName, createdAt: now.AddMinutes(-2));
    TestDbHelper.CreateInviteCodeEntity(
      "second-list@example.com",
      RegisteredApp.AppName,
      applied: true,
      createdAt: now.AddMinutes(-1));
    TestDbHelper.CreateInviteCodeEntity(
      "third-list@example.com",
      RegisteredApp.AppName,
      role: OwnerRole,
      createdAt: now);
    TestDbHelper.CreateInviteCodeEntity("other-app-list@example.com", TestingWebAppFactory.OtherAppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}?page=1&pageSize=2");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal(3, jsonObject["totalCount"]?.Value<int>());

    var inviteCodes = jsonObject["inviteCodes"]?.ToObject<List<InviteCodeDto>>();

    Assert.NotNull(inviteCodes);
    Assert.Equal(["third-list@example.com", "second-list@example.com"], inviteCodes.Select(i => i.UserEmail));
    Assert.Equal(OwnerRole, inviteCodes[0].Role);
    Assert.True(inviteCodes[1].Applied);
    Assert.NotNull(inviteCodes[0].CreatedAt);
  }

  [Fact]
  public async Task Get_InviteCodes_With_Page_Zero_Returns_Validation_Error()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}?page=0");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

    var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

    Assert.NotNull(errorList);
    var error = Assert.Single(errorList);
    Assert.Equal("page", error.Field);
    Assert.Equal("The field page must be between 1 and 21474836.", error.Message);
  }

  [Fact]
  public async Task Get_InviteCodes_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    TestDbHelper.SeedDatabase();

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  #endregion

  #region DeleteInviteCode Tests

  [Fact]
  public async Task Delete_Unused_InviteCode_Revokes_It()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = TestDbHelper.CreateInviteCodeEntity("revoke@example.com", RegisteredApp.AppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  [Fact]
  public async Task Delete_Used_InviteCode_Returns_BadRequest()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = TestDbHelper.CreateInviteCodeEntity(
      "revoke-used@example.com",
      RegisteredApp.AppName,
      applied: true);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.NotNull(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  [Fact]
  public async Task Delete_Another_Apps_InviteCode_Returns_NotFound()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
    TestDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = TestDbHelper.CreateInviteCodeEntity(
      "revoke-other-app@example.com",
      TestingWebAppFactory.OtherAppName);

    // Act
    var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.NotNull(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  #endregion
}
