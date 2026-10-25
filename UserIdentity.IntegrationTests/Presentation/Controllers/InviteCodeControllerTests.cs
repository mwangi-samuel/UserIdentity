using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using UserIdentity.Domain.InviteCodes;
using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Controllers;

public class InviteCodeControllerTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly static string _baseUri = "/api/v1/invitecode";
  private const string OwnerRole = "testapp:owner";

  #region GetInviteCode Tests

  [Fact]
  public async Task Get_InviteCode_By_Id_Returns_InviteCode()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "invitecode-test@example.com";
    var inviteCode = _testDbHelper.CreateInviteCodeEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

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
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "invitecode-email-test@example.com";
    var inviteCode = _testDbHelper.CreateInviteCodeEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/email/{testEmail}");

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
    _testDbHelper.SeedDatabase();

    var testEmail = "invitecode-forbidden@example.com";
    var inviteCode = _testDbHelper.CreateInviteCodeEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Get_InviteCode_With_No_Auth_Returns_Unauthorized()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var testEmail = "invitecode-noauth@example.com";
    var inviteCode = _testDbHelper.CreateInviteCodeEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Get_Non_Existing_InviteCode_Returns_NotFound()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var nonExistingId = 99999L;

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{nonExistingId}");

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
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = "TESTCODE";
    var userEmail = "new-invitecode@example.com";

    var requestPayload = new
    {
      inviteCode,
      userEmail
    };

    // Clean up any existing record
    _testDbHelper.DeleteInviteCodeEntityByEmail(userEmail);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    _outputHelper.WriteLine(responseString);

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
    var savedEntity = _testDbHelper.GetInviteCodeEntityByEmail(userEmail);
    Assert.NotNull(savedEntity);
    Assert.Equal(inviteCode, savedEntity.InviteCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      InviteCode = "FORBCODE",
      UserEmail = "forbidden-invitecode@example.com"
    };

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_No_Auth_Returns_Unauthorized()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      InviteCode = "NOAUTHCODE",
      UserEmail = "noauth-invitecode@example.com"
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Create_InviteCode_With_Role_Returns_The_Role()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    _testDbHelper.CreateIdentityRole(OwnerRole);

    var userEmail = "owner-invitecode@example.com";

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, new { inviteCode = "OWNERCODE", userEmail, role = OwnerRole });

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var inviteCodeDetails = SerDeHelper.Deserialize<JObject>(responseString)?["inviteCode"]?.ToObject<InviteCodeDto>();

    Assert.NotNull(inviteCodeDetails);
    Assert.Equal(OwnerRole, inviteCodeDetails.Role);
    Assert.False(inviteCodeDetails.Applied);
    Assert.Equal(OwnerRole, _testDbHelper.GetInviteCodeEntityByEmail(userEmail)?.Role);
  }

  [Theory]
  [InlineData("testapp:admin")]
  [InlineData("otherapp:owner")]
  [InlineData("testapp:missing")]
  public async Task Create_InviteCode_With_An_Admin_Another_Apps_Or_A_Missing_Role_Returns_BadRequest(string role)
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();
    _testDbHelper.CreateIdentityRole("otherapp:owner");

    var userEmail = "bad-role-invitecode@example.com";

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, new { inviteCode = "BADROLE", userEmail, role });

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Null(_testDbHelper.GetInviteCodeEntityByEmail(userEmail));
  }

  #endregion

  #region GetInviteCodes Tests

  [Fact]
  public async Task Get_InviteCodes_Returns_The_Apps_InviteCodes_Newest_First()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var now = DateTime.UtcNow;
    _testDbHelper.CreateInviteCodeEntity("first-list@example.com", _registeredApp.AppName, createdAt: now.AddMinutes(-2));
    _testDbHelper.CreateInviteCodeEntity("second-list@example.com", _registeredApp.AppName, applied: true, createdAt: now.AddMinutes(-1));
    _testDbHelper.CreateInviteCodeEntity("third-list@example.com", _registeredApp.AppName, role: OwnerRole, createdAt: now);
    _testDbHelper.CreateInviteCodeEntity("other-app-list@example.com", TestingWebAppFactory.OtherAppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}?page=1&pageSize=2");

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
  public async Task Get_InviteCodes_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri);

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
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = _testDbHelper.CreateInviteCodeEntity("revoke@example.com", _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null(_testDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  [Fact]
  public async Task Delete_Used_InviteCode_Returns_BadRequest()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = _testDbHelper.CreateInviteCodeEntity("revoke-used@example.com", _registeredApp.AppName, applied: true);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.NotNull(_testDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  [Fact]
  public async Task Delete_Another_Apps_InviteCode_Returns_NotFound()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var inviteCode = _testDbHelper.CreateInviteCodeEntity("revoke-other-app@example.com", TestingWebAppFactory.OtherAppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Delete, $"{_baseUri}/{inviteCode.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.NotNull(_testDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id));
  }

  #endregion
}
