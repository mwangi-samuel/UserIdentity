using System.Net;
using Newtonsoft.Json.Linq;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Domain.WaitLists;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class WaitListEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private static readonly string _baseUri = "/api/v1/wait-list";

    #region GetWaitList Tests

    [Fact]
    public async Task Get_WaitList_By_Id_Returns_WaitList()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var testEmail = "waitlist-test@example.com";
        var waitList = TestDbHelper.CreateWaitListEntity(testEmail, RegisteredApp.AppName);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

        var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDto>();

        Assert.NotNull(waitListDetails);
        Assert.Equal(waitList.Id, waitListDetails.Id);
        Assert.Equal(testEmail, waitListDetails.UserEmail);
    }

    [Fact]
    public async Task Get_WaitList_By_Email_Returns_WaitList()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var testEmail = "waitlist-email-test@example.com";
        var waitList = TestDbHelper.CreateWaitListEntity(testEmail, RegisteredApp.AppName);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/email/{testEmail}");

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

        var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDto>();

        Assert.NotNull(waitListDetails);
        Assert.Equal(waitList.Id, waitListDetails.Id);
        Assert.Equal(testEmail, waitListDetails.UserEmail);
    }

    [Fact]
    public async Task Get_WaitList_With_Non_Admin_User_Returns_Forbidden()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var testEmail = "waitlist-forbidden@example.com";
        var waitList = TestDbHelper.CreateWaitListEntity(testEmail, RegisteredApp.AppName);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WaitList_With_No_Auth_Returns_Unauthorized()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var testEmail = "waitlist-noauth@example.com";
        var waitList = TestDbHelper.CreateWaitListEntity(testEmail, RegisteredApp.AppName);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, $"{_baseUri}/{waitList.Id}");

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Non_Existing_WaitList_Returns_NotFound()
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

    #region CreateWaitList Tests

    [Fact]
    public async Task Create_WaitList_With_Anonymous_User_Creates_And_Returns_WaitList()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var newEmail = "new-waitlist@example.com";

        var requestPayload = new { UserEmail = newEmail };

        // Clean up any existing record
        TestDbHelper.DeleteWaitListEntityByEmail(newEmail);

        // Act - WaitList allows anonymous creation
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDto>();

        Assert.NotNull(waitListDetails);
        Assert.Equal(newEmail, waitListDetails.UserEmail);
        Assert.True(waitListDetails.Id > 0);

        // Verify in database
        var savedEntity = TestDbHelper.GetWaitListEntityByEmail(newEmail);
        Assert.NotNull(savedEntity);
        Assert.Equal(newEmail, savedEntity.UserEmail);
    }

    [Fact]
    public async Task Create_WaitList_With_Authenticated_User_Creates_And_Returns_WaitList()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var newEmail = "authenticated-waitlist@example.com";

        var requestPayload = new { UserEmail = newEmail };

        // Clean up any existing record
        TestDbHelper.DeleteWaitListEntityByEmail(newEmail);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var waitListDetails = jsonObject["waitList"]?.ToObject<WaitListDto>();

        Assert.NotNull(waitListDetails);
        Assert.Equal(newEmail, waitListDetails.UserEmail);
        Assert.True(waitListDetails.Id > 0);
    }

    [Fact]
    public async Task Create_WaitList_Without_Email_Returns_BadRequest()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserEmail = (string?)null };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WaitList_Ignores_Fields_The_Server_Sets()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var newEmail = "mass-assignment@example.com";
        var otherApp = TestDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName);

        var requestPayload = new
        {
            Id = 999999,
            UserEmail = newEmail,
            AppId = otherApp.Id,
            Applied = true,
            IsDeleted = true,
            CreatedBy = "attacker",
            UpdatedBy = "attacker",
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var savedEntity = TestDbHelper.GetWaitListEntityByEmail(newEmail);

        Assert.NotNull(savedEntity);
        Assert.NotEqual(999999, savedEntity.Id);
        Assert.Equal(TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id, savedEntity.AppId);
        Assert.False(savedEntity.IsDeleted);
        Assert.NotEqual("attacker", savedEntity.CreatedBy);
        Assert.NotEqual("attacker", savedEntity.UpdatedBy);
    }

    [Fact]
    public async Task Create_WaitList_With_A_Malformed_Email_Returns_BadRequest_With_Error_List()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserEmail = "not-an-email" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri, requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorList = SerDeHelper.Deserialize<JObject>(responseString)?["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.NotNull(errorList);
        Assert.Contains(errorList, error => error.Field == "UserEmail");
        Assert.Null(TestDbHelper.GetWaitListEntityByEmail("not-an-email"));
    }

    #endregion
}
