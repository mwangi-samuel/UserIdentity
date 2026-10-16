using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using UserIdentity.Domain.WaitLists;
using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Controllers;

public class WaitListControllerTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly static string _baseUri = "/api/v1/waitlist";

  #region GetWaitList Tests

  [Fact]
  public async Task Get_WaitList_By_Id_Returns_WaitList()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "waitlist-test@example.com";
    var waitList = _testDbHelper.CreateWaitListEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

    var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDTO>();

    Assert.NotNull(waitListDetails);
    Assert.Equal(waitList.Id, waitListDetails.Id);
    Assert.Equal(testEmail, waitListDetails.UserEmail);
  }

  [Fact]
  public async Task Get_WaitList_By_Email_Returns_WaitList()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var testEmail = "waitlist-email-test@example.com";
    var waitList = _testDbHelper.CreateWaitListEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/email/{testEmail}");

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

    var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDTO>();

    Assert.NotNull(waitListDetails);
    Assert.Equal(waitList.Id, waitListDetails.Id);
    Assert.Equal(testEmail, waitListDetails.UserEmail);
  }

  [Fact]
  public async Task Get_WaitList_With_Non_Admin_User_Returns_Forbidden()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var testEmail = "waitlist-forbidden@example.com";
    var waitList = _testDbHelper.CreateWaitListEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Get_WaitList_With_No_Auth_Returns_Unauthorized()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var testEmail = "waitlist-noauth@example.com";
    var waitList = _testDbHelper.CreateWaitListEntity(testEmail, _registeredApp.AppName);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Get_Non_Existing_WaitList_Returns_NotFound()
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

  #region CreateWaitList Tests

  [Fact]
  public async Task Create_WaitList_With_Anonymous_User_Creates_And_Returns_WaitList()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var newEmail = "new-waitlist@example.com";

    var requestPayload = new
    {
      UserEmail = newEmail
    };

    // Clean up any existing record
    _testDbHelper.DeleteWaitListEntityByEmail(newEmail);

    // Act - WaitList allows anonymous creation
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

    var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDTO>();

    Assert.NotNull(waitListDetails);
    Assert.Equal(newEmail, waitListDetails.UserEmail);
    Assert.True(waitListDetails.Id > 0);

    // Verify in database
    var savedEntity = _testDbHelper.GetWaitListEntityByEmail(newEmail);
    Assert.NotNull(savedEntity);
    Assert.Equal(newEmail, savedEntity.UserEmail);
  }

  [Fact]
  public async Task Create_WaitList_With_Authenticated_User_Creates_And_Returns_WaitList()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var newEmail = "authenticated-waitlist@example.com";

    var requestPayload = new
    {
      UserEmail = newEmail
    };

    // Clean up any existing record
    _testDbHelper.DeleteWaitListEntityByEmail(newEmail);

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);
    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

    var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDTO>();

    Assert.NotNull(waitListDetails);
    Assert.Equal(newEmail, waitListDetails.UserEmail);
    Assert.True(waitListDetails.Id > 0);
  }

  [Fact]
  public async Task Create_WaitList_Without_Email_Returns_BadRequest()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserEmail = (string?)null
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  #endregion
}
