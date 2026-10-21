using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.IdentityModel.Tokens;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Kafka;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Presentation.ValidationHelpers;

using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Presentation.Settings;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Controllers;

public class UserControllerTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{

  private readonly static string _baseUri = "/api/v1/user";

  private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

  private readonly WireMockServer _wireMock = testingWebAppFactory.WireMockServer;

  [Fact]
  public async Task Get_Existing_User_Gets_User_Details()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

    var userDetails = jsonObject["user"]?.ToObject<UserDTO>();

    Assert.Equal(UserSettingHelper.FirstName, userDetails?.FirstName);
    Assert.Equal(UserSettingHelper.UserName, userDetails?.UserName);
    Assert.Equal(UserSettingHelper.UserEmail, userDetails?.Email);
    Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
    Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);
  }

  [Fact]
  public async Task Get_Non_Existing_User_Returns_Forbidden_Error_For_Unauthorized_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var nonExistingUserId = Guid.NewGuid().ToString();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.Null(jsonObject);
  }

  [Fact]
  public async Task Get_Non_Existing_User_Does_Not_Get_User_Details_For_Authorized_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    var nonExistingUserId = Guid.NewGuid().ToString();

    // Act
    var response = await _httpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

    Assert.Equal($"No record exists for the provided identifier - {nonExistingUserId}", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Get_Deleted_AppUser_Does_Not_Get_User_Details_For_UnAuthorized_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, _) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    _testDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);
    httpRequest.AddAuthHeader(userToken);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

    Assert.Equal($"No record exists for the provided identifier - {UserSettingHelper.UserId}", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Get_Deleted_AppUser_Does_Not_Get_User_Details_For_Authorized_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.CreateIdentityRole(_testDbHelper.AdminRoles.First());
    _testDbHelper.ConfigureIdentityUserAsAdmin();

    (var userToken, _) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    _testDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

    var nonExistingUserId = Guid.NewGuid().ToString();

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);
    httpRequest.AddAuthHeader(userToken);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

    Assert.Equal($"No record exists for the provided identifier - {nonExistingUserId}", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Register_User_With_Invalid_Request_Payload_Returns_Validation_Errors()
  {
    // Arrange
    var requestPayload = new
    {
      UserSettingHelper.LastName,
      UserSettingHelper.PhoneNumber,
      UserSettingHelper.UserEmail
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Validation Failed", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);

    var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

    Assert.Equal(3, errorList?.Count);

    Assert.True(errorList?.Any(x => x.Field == "FirstName" && x.Message == "The FirstName field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "UserName" && x.Message == "The UserName field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "UserPassword" && x.Message == "The UserPassword field is required.") ?? false);
  }

  [Fact]
  public async Task Register_User_With_Invalid_Request_Missing_Email_Phone_Number_Payload_Returns_Validation_Errors()
  {
    // Arrange
    var requestPayload = new
    {
      UserSettingHelper.FirstName,
      UserSettingHelper.UserName,
      UserSettingHelper.UserPassword
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Validation Failed", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);

    var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

    Assert.Equal(2, errorList?.Count);

    Assert.True(errorList?.Any(x => x.Field == "UserEmail" && x.Message == "Either PhoneNumber or UserEmail must be provided.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "PhoneNumber" && x.Message == "Either PhoneNumber or UserEmail must be provided.") ?? false);
  }

  [Fact]
  public async Task Register_User_With_Valid_Request_Payload_Registers_User()
  {
    // Arrange
    var googleRecaptchaToken = StringUtil.GenerateRandomString(90);

    var requestPayload = new
    {
      UserSettingHelper.FirstName,
      UserSettingHelper.LastName,
      UserSettingHelper.UserName,
      UserSettingHelper.PhoneNumber,
      UserSettingHelper.UserEmail,
      UserSettingHelper.UserPassword,
      googleRecaptchaToken
    };

    SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

    var userDetails = jsonObject["user"]?.ToObject<UserDTO>();

    Assert.Equal(requestPayload.FirstName, userDetails?.FirstName);
    Assert.Equal(requestPayload.UserName, userDetails?.UserName);
    Assert.Equal(requestPayload.UserEmail, userDetails?.Email);
    Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
    Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

    var userToken = jsonObject["userToken"]?.ToObject<AccessTokenViewModel>();

    Assert.Null(userToken);

    Assert.False(jsonObject["isConfirmed"]?.Value<bool>() ?? false);

    VerifyGoogleRecaptchaEndpoint();

    // Confirm produced message 
    var kafkaMessage = null as MessageEvent;

    await _pollyResiliencePipeline.ExecuteAsync(async token =>
    {
      kafkaMessage = await _kafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync(userDetails!.Id);
      Assert.NotNull(kafkaMessage);
    }, TestContext.Current.CancellationToken);

    Assert.Equal(userDetails?.Id, kafkaMessage!.CorrelationId);
    Assert.Equal(_registeredApp.AppName, kafkaMessage.RegisteredApp);
    Assert.Equal(MessageAction.WELCOME_USER, kafkaMessage.Action);

    var messageAttributes = kafkaMessage.Attributes;
    Assert.NotNull(messageAttributes);

    Assert.Equal(requestPayload.UserEmail, messageAttributes.GetAttributeValue(MessageAttribute.RECIPIENT_EMAIL));
    Assert.Equal(requestPayload.PhoneNumber, messageAttributes.GetAttributeValue(MessageAttribute.RECIPIENT_PHONE));
    Assert.Equal(requestPayload.FirstName, messageAttributes.GetAttributeValue(MessageAttribute.FIRST_NAME));
    Assert.Equal(requestPayload.LastName, messageAttributes.GetAttributeValue(MessageAttribute.LAST_NAME));
    Assert.Equal(requestPayload.UserName, messageAttributes.GetAttributeValue(MessageAttribute.USER_NAME));
    Assert.Equal(userDetails?.Id, messageAttributes.GetAttributeValue(MessageAttribute.USER_ID));
  }

  [Theory]
  [InlineData("random@test.com", null)]
  [InlineData("random@test.com", "")]
  [InlineData(null, "712121212")]
  [InlineData("", "712121212")]
  public async Task Register_User_With_Only_Required_Request_Payload_Registers_User(string? UserEmail, string? PhoneNumber)
  {
    // Arrange
    var googleRecaptchaToken = StringUtil.GenerateRandomString(90);

    var requestPayload = new
    {
      UserSettingHelper.FirstName,
      UserSettingHelper.UserName,
      UserSettingHelper.UserPassword,
      UserEmail,
      PhoneNumber,
      googleRecaptchaToken
    };

    SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

    var userDetails = jsonObject["user"]?.ToObject<UserDTO>();

    Assert.Equal(requestPayload.FirstName, userDetails?.FirstName);
    Assert.Equal(requestPayload.UserName, userDetails?.UserName);
    Assert.Equal(UserEmail, userDetails?.Email);
    Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
    Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

    var userToken = jsonObject["userToken"]?.ToObject<AccessTokenViewModel>();

    Assert.Null(userToken);

    Assert.False(jsonObject["isConfirmed"]?.Value<bool>() ?? false);

    VerifyGoogleRecaptchaEndpoint();

    // Confirm produced message 
    var kafkaMessage = null as MessageEvent;

    await _pollyResiliencePipeline.ExecuteAsync(async token =>
    {
      kafkaMessage = await _kafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync(userDetails!.Id);
      Assert.NotNull(kafkaMessage);
    }, TestContext.Current.CancellationToken);

    Assert.Equal(userDetails?.Id, kafkaMessage!.CorrelationId);
    Assert.Equal(_registeredApp.AppName, kafkaMessage.RegisteredApp);
    Assert.Equal(MessageAction.WELCOME_USER, kafkaMessage.Action);

    var messageAttributes = kafkaMessage.Attributes;
    Assert.NotNull(messageAttributes);

    Assert.Equal(requestPayload.UserEmail ?? string.Empty, messageAttributes.GetAttributeValue(MessageAttribute.RECIPIENT_EMAIL));
    Assert.Equal(requestPayload.PhoneNumber ?? string.Empty, messageAttributes.GetAttributeValue(MessageAttribute.RECIPIENT_PHONE));
    Assert.Equal(requestPayload.FirstName, messageAttributes.GetAttributeValue(MessageAttribute.FIRST_NAME));
    Assert.Equal(string.Empty, messageAttributes.GetAttributeValue(MessageAttribute.LAST_NAME));
    Assert.Equal(requestPayload.UserName, messageAttributes.GetAttributeValue(MessageAttribute.USER_NAME));
    Assert.Equal(userDetails?.Id, messageAttributes.GetAttributeValue(MessageAttribute.USER_ID));
  }

  [Fact]
  public async Task Register_Existing_User_With_Valid_Request_Payload_Does_Not_Create_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserSettingHelper.FirstName,
      UserSettingHelper.LastName,
      UserSettingHelper.UserName,
      UserSettingHelper.PhoneNumber,
      UserSettingHelper.UserEmail,
      UserSettingHelper.UserPassword

    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

    Assert.Equal($"A record identified with - {requestPayload.UserName} - exists", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Login_Existing_User_Logs_In_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Login successful", $"{jsonObject["statusMessage"]}");

    var userDetails = jsonObject["user"]?.ToObject<UserDTO>();

    Assert.Equal(UserSettingHelper.FirstName, userDetails?.FirstName);
    Assert.Equal(requestPayload.UserName, userDetails?.UserName);
    Assert.Equal(UserSettingHelper.UserEmail, userDetails?.Email);
    Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
    Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

    var userToken = jsonObject["userToken"]?.ToObject<AccessTokenViewModel>();

    Assert.NotNull(userToken);
    Assert.IsType<AccessTokenDTO>(userToken.AccessToken);
  }

  [Fact]
  public async Task Login_Non_Existing_User_Does_Not_Log_In_User()
  {
    // Arrange
    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Provided credentials are invalid", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Login_Existing_Non_Existent_AppUser_Does_Not_Log_In_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Provided credentials are invalid", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Login_Existing_Deleted_AppUser_Does_Not_Log_In_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    _testDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Provided credentials are invalid", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Login_Existing_With_Invalid_Password_Does_Not_Log_In_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword + "123"
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Provided credentials are invalid", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Refresh_Valid_User_Token_Refreshes_User_token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var requestPayload = new
    {
      AccessToken = userToken,
      RefreshToken = refreshToken
    };

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();
    _outputHelper.WriteLine(responseString);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Refresh token generated successfully", $"{jsonObject["statusMessage"]}");

    var userTokenVM = jsonObject["userToken"]?.ToObject<AccessTokenViewModel>();

    Assert.NotNull(userTokenVM);
    Assert.IsType<AccessTokenDTO>(userTokenVM.AccessToken);

    Assert.NotEqual(userToken, userTokenVM.AccessToken.Token);
    Assert.NotEqual(refreshToken, userTokenVM.RefreshToken);

    Assert.True(userTokenVM?.AccessToken.ExpiresIn > 0);
  }

  [Fact]
  public async Task Refresh_User_Token_With_Invalid_Refresh_Token_Does_Not_Refresh_User_token()
  {
    // Arrange// Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var requestPayload = new
    {
      AccessToken = userToken,
      RefreshToken = Base64UrlEncoder.Encode(refreshToken)
    };

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Invalid refresh token provided", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Refresh_User_Token_With_Deleted_Refresh_Token_Does_Not_Refresh_User_token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    _testDbHelper.DeleteRefreshTokenEntity(UserSettingHelper.UserId);

    var requestPayload = new
    {
      AccessToken = userToken,
      RefreshToken = Base64UrlEncoder.Encode(refreshToken)
    };

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Invalid refresh token provided", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Refresh_User_Token_With_Invalid_Access_Token_Does_Not_Refresh_User_token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (_, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var requestPayload = new
    {
      AccessToken = UserSettingHelper.InvalidUserToken,
      RefreshToken = refreshToken
    };

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

    Assert.Equal("An invalid access token was provided", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }

  [Fact]
  public async Task Login_Stores_Only_The_Hash_Of_The_Refresh_Token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    // Act
    (_, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    // Assert
    var refreshTokenEntities = _testDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId);

    Assert.DoesNotContain(refreshTokenEntities, r => r.Token == refreshToken);
    Assert.Contains(refreshTokenEntities, r => r.Token == new TokenFactory().HashToken(refreshToken) && r.Expires > DateTime.UtcNow.AddDays(29));
  }

  [Fact]
  public async Task Login_After_Too_Many_Failed_Attempts_Locks_Out_User()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var user = _testDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
    user.LockoutEnabled = true;
    _testDbHelper.UpdateIdentityUser(user);

    var wrongPasswordPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword + "123"
    };

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    for (var i = 0; i < 5; i++)
      await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", wrongPasswordPayload);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Provided credentials are invalid", jsonObject["error"]?["message"]);

    var lockedOutUser = _testDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

    Assert.NotNull(lockedOutUser?.LockoutEnd);
    Assert.True(lockedOutUser.LockoutEnd > DateTimeOffset.UtcNow);
  }

  [Fact]
  public async Task Login_Beyond_Rate_Limit_Returns_Too_Many_Requests()
  {
    // Arrange - rate limiting is off in the shared test host
    await using var rateLimitedFactory = _testingWebAppFactory.WithWebHostBuilder(builder =>
    {
      builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "true");
      builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.PermitLimit)}", "2");
      builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.WindowSeconds)}", "60");
      builder.UseSetting($"{nameof(KafkaSettings)}:{nameof(KafkaSettings.ConsumerEnabled)}", "false");
    });

    using var httpClient = rateLimitedFactory.CreateClient();

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserName,
      Password = UserSettingHelper.UserPassword
    };

    // Act
    var allowedResponses = new List<HttpResponseMessage>
    {
      await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload),
      await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload)
    };

    var response = await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

    // Assert
    Assert.All(allowedResponses, r => Assert.NotEqual(HttpStatusCode.TooManyRequests, r.StatusCode));

    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    Assert.True(response.Headers.Contains("Retry-After"));

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("429 - TOO MANY REQUESTS", $"{jsonObject["statusMessage"]}");
    Assert.Equal("Too many requests, try again later", jsonObject["error"]?["message"]);
  }

  [Fact]
  public async Task Refresh_User_Token_With_Expired_Refresh_Token_Does_Not_Refresh_User_Token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    _testDbHelper.ExpireRefreshTokenEntities(UserSettingHelper.UserId);

    var requestPayload = new
    {
      AccessToken = userToken,
      RefreshToken = refreshToken
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Expired refresh token provided", jsonObject["error"]?["message"]);

    Assert.Empty(_testDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task Refresh_User_Token_With_Already_Used_Refresh_Token_Ends_Session()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var requestPayload = new
    {
      AccessToken = userToken,
      RefreshToken = refreshToken
    };

    var firstResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

    Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

    var userTokenVM = SerDeHelper.Deserialize<JObject>(await firstResponse.ValidateRequestResponseAsync())?["userToken"]?.ToObject<AccessTokenViewModel>();

    Assert.NotNull(userTokenVM?.RefreshToken);

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Invalid refresh token provided", jsonObject["error"]?["message"]);

    var newTokenResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", new
    {
      AccessToken = userTokenVM.AccessToken?.Token,
      userTokenVM.RefreshToken
    });

    Assert.Equal(HttpStatusCode.Unauthorized, newTokenResponse.StatusCode);
  }

  [Fact]
  public async Task Logout_User_Ends_Session()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/logout");
    httpRequest.AddAuthHeader(userToken);
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(new { RefreshToken = refreshToken });

    // Act
    var response = await _httpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Logout successful", $"{jsonObject["statusMessage"]}");

    var refreshResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", new
    {
      AccessToken = userToken,
      RefreshToken = refreshToken
    });

    Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
  }

  [Fact]
  public async Task Logout_User_Without_Access_Token_Returns_Unauthorized()
  {
    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/logout", new { RefreshToken = StringUtil.GenerateRandomString(32) });

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Reset_Password_With_Invalid_Payload_Returns_Validation_Results()
  {
    // Arrange
    var requestPayload = new
    {
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Validation Failed", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);

    var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

    Assert.Equal(1, errorList?.Count);

    Assert.True(errorList?.Any(x => x.Field == "UserName" && x.Message == "The UserName field is required.") ?? false);
  }

  [Fact]
  public async Task Reset_Password_For_Existing_User_Creates_Reset_Request()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserName = UserSettingHelper.UserEmail,
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Password reset request successful", $"{jsonObject["statusMessage"]}");

    var resetPasswordDTO = jsonObject["resetPasswordDetails"]?.ToObject<ResetPasswordDTO>();

    Assert.NotNull(resetPasswordDTO?.ResetRequestId);

    var passwordResetEntity = _testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId);

    Assert.NotNull(passwordResetEntity);
    Assert.Equal(new TokenFactory().HashToken(resetPasswordDTO.ResetRequestId), passwordResetEntity.RequestId);
    Assert.False(passwordResetEntity.Verified);
    Assert.False(passwordResetEntity.Applied);
  }

  [Fact]
  public async Task Reset_Password_For_Non_Existing_User_Returns_Same_Response_Without_Creating_Reset_Request()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserName = "hello@test.com",
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Password reset request successful", $"{jsonObject["statusMessage"]}");

    var resetPasswordDTO = jsonObject["resetPasswordDetails"]?.ToObject<ResetPasswordDTO>();

    Assert.NotNull(resetPasswordDTO?.ResetRequestId);
    Assert.Null(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task Reset_Password_For_Non_Existing_UserDetails_Record_Does_Not_Create_Reset_Request()
  {
    // Arrange
    _testDbHelper.CreateIdentityUser();

    var requestPayload = new
    {
      UserSettingHelper.UserName
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

    // Assert
    await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task Reset_Password_With_Verified_Code_Updates_Password_And_Ends_Sessions()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    (var userToken, var refreshToken) = await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var resetResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", new { UserSettingHelper.UserName });
    var resetRequestId = SerDeHelper.Deserialize<JObject>(await resetResponse.ValidateRequestResponseAsync())?["resetPasswordDetails"]?["resetRequestId"]?.ToString();

    Assert.NotNull(resetRequestId);

    // Simulate MessageManager verifying the code
    var attributes = new Dictionary<MessageAttribute, string>()
    {
      { MessageAttribute.USER_ID, resetRequestId }
    };

    var messageEvent = MessageEventFactory.Create(resetRequestId, _registeredApp.AppName, attributes, MessageAction.PASSWORD_RESET, CrudEvent.UPDATE);

    await _kafkaProducerHelper.ProduceUserMessageManagerUserUpdatedEventAsync(resetRequestId, messageEvent);

    await _pollyResiliencePipeline.ExecuteAsync(token =>
    {
      Assert.True(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Verified);
      return ValueTask.CompletedTask;
    }, TestContext.Current.CancellationToken);

    var newPassword = "N3wP@ssword!";

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", new { ResetRequestId = resetRequestId, NewPassword = newPassword });

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Password updated successfully", $"{jsonObject["statusMessage"]}");
    Assert.True(jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDTO>()?.PassWordUpdated);

    Assert.True(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);

    await _httpClient.LoginUserAsync(UserSettingHelper.UserName, newPassword);

    var refreshResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", new
    {
      AccessToken = userToken,
      RefreshToken = refreshToken
    });

    Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

    var reuseResponse = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", new { ResetRequestId = resetRequestId, NewPassword = "An0therP@ssword!" });

    Assert.Equal(HttpStatusCode.NotAcceptable, reuseResponse.StatusCode);
  }

  [Fact]
  public async Task Update_Password_With_Invalid_Payload_Returns_Validation_Results()
  {
    // Arrange
    var requestPayload = new
    {
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

    Assert.Equal("Validation Failed", jsonObject["error"]?["message"]);

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);

    var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

    Assert.Equal(2, errorList?.Count);

    Assert.True(errorList?.Any(x => x.Field == "NewPassword" && x.Message == "The NewPassword field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "ResetRequestId" && x.Message == "The ResetRequestId field is required.") ?? false);
  }

  [Fact]
  public async Task Update_Password_With_Unknown_Reset_Request_Does_Not_Update_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      ResetRequestId = StringUtil.GenerateRandomString(32),
      NewPassword = "N3wP@ssword!"
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Password update failed", $"{jsonObject["statusMessage"]}");

    var updatePasswordDTO = jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDTO>();

    Assert.NotNull(updatePasswordDTO);
    Assert.False(updatePasswordDTO?.PassWordUpdated);
  }

  [Theory]
  [InlineData(false, false, 15)]
  [InlineData(true, true, 15)]
  [InlineData(true, false, -1)]
  public async Task Update_Password_With_Unverified_Used_Or_Expired_Reset_Request_Does_Not_Update_Password(bool verified, bool applied, int expiresInMinutes)
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var resetRequestId = StringUtil.GenerateRandomString(32);
    _testDbHelper.CreatePasswordResetEntity(resetRequestId, verified, applied, DateTime.UtcNow.AddMinutes(expiresInMinutes));

    var requestPayload = new
    {
      ResetRequestId = resetRequestId,
      NewPassword = "N3wP@ssword!"
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Password update failed", $"{jsonObject["statusMessage"]}");
    Assert.False(jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDTO>()?.PassWordUpdated);

    await _httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);
  }

  [Fact]
  public async Task Update_Password_With_Password_Not_Meeting_Rules_Returns_Validation_Errors()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var resetRequestId = StringUtil.GenerateRandomString(32);
    _testDbHelper.CreatePasswordResetEntity(resetRequestId);

    var requestPayload = new
    {
      ResetRequestId = resetRequestId,
      NewPassword = "12345"
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");
    Assert.Contains("Passwords must be at least 8 characters.", $"{jsonObject["error"]?["message"]}");

    Assert.False(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
  }

  private void SetUpGoogleRecaptchaEndpoint(string googleRecaptchaToken)
  {

    var googleRecaptchaSettings = _configuration.GetSetting<GoogleRecaptchaSettings>();

    var googleRecaptchaResponse = new
    {
      success = true,
      score = 0.9f
    };

    _wireMock.Reset();
    _wireMock.Given(
      Request.Create()
      .WithPath("/siteverify")
      .WithParam("secret", googleRecaptchaSettings.SiteKey!)
      .WithParam("response", googleRecaptchaToken)
      .UsingPost()
    ).RespondWith(
      Response.Create()
      .WithStatusCode(HttpStatusCode.OK)
      .WithBody(JsonConvert.SerializeObject(googleRecaptchaResponse))
     );

  }

  private void VerifyGoogleRecaptchaEndpoint()
  {
    // verify calls to wiremock
    var logs = _wireMock.LogEntries;
    Assert.Contains(logs, l => l.RequestMessage?.Path == "/siteverify");
  }
}
