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

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDTO>();

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

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDTO>();

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

    var inviteCodeDetails = jsonObject["inviteCode"]?.ToObject<InviteCodeDTO>();

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
  #endregion
}
