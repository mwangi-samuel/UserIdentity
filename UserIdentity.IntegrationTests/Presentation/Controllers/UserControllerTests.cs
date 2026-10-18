using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.ValidationHelpers;

using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Domain.Users;
using UserIdentity.IntegrationTests.TestUtils;

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
      kafkaMessage = await _kafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync<MessageEvent>();
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
      kafkaMessage = await _kafkaConsumerHelper.WaitAndConsumeUserUpdatedMessageAsync<MessageEvent>();
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

    Assert.True(errorList?.Any(x => x.Field == "UserEmail" && x.Message == "The UserEmail field is required.") ?? false);
  }

  [Fact]
  public async Task Reset_Password_For_Existing_User_Resets_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserSettingHelper.UserEmail,
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

    Assert.NotNull(resetPasswordDTO);
  }

  [Fact]
  public async Task Reset_Password_For_Non_Existing_User_Returns_Success_Message()
  {
    // Arrange
    _testDbHelper.SeedDatabase();

    var requestPayload = new
    {
      UserEmail = "hello@test.com",
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

    Assert.NotNull(resetPasswordDTO);
  }

  [Fact]
  public async Task Reset_Password_For_Non_Existing_UserDetails_Record_Does_Not_Reset_Password()
  {
    // Arrange
    _testDbHelper.CreateIdentityUser();

    var requestPayload = new
    {
      UserSettingHelper.UserEmail
    };


    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);


    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

    Assert.StartsWith("No record exists for the provided identifier - ", $"{jsonObject["error"]?["message"]}");

    var dateTime = (DateTime?)jsonObject["error"]?["timestamp"];

    Assert.NotNull(dateTime);
    Assert.Equal(DateTime.UtcNow.Year, dateTime.Value.Year);
    Assert.Equal(DateTime.UtcNow.Month, dateTime.Value.Month);
    Assert.Equal(DateTime.UtcNow.Day, dateTime.Value.Day);
  }


  [Fact]
  public async Task Confirm_Password_Token_With_Invalid_Payload_Returns_Validation_Results()
  {
    // Arrange
    var requestPayload = new
    {
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm-update-password-token", requestPayload);

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

    Assert.True(errorList?.Any(x => x.Field == "ConfirmPasswordToken" && x.Message == "The ConfirmPasswordToken field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "UserId" && x.Message == "The UserId field is required.") ?? false);
  }

  [Fact]
  public async Task Confirm_Password_Token_With_Valid_Payload_Confirms_Password_Token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      ConfirmPasswordToken = resetPasswordToken,
      UserSettingHelper.UserId
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm-update-password-token", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Token confirmation successful", $"{jsonObject["statusMessage"]}");


    var resetPasswordDTO = jsonObject["tokenPasswordResult"]?.ToObject<ConfirmUpdatePasswordDTO>();

    Assert.NotNull(resetPasswordDTO);
    Assert.True(resetPasswordDTO?.UpdatePasswordTokenConfirmed);
  }


  [Fact]
  public async Task Confirm_Password_Token_With_Invalid_Token_Does_Not_Confirm_Password_Token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      ConfirmPasswordToken = resetPasswordToken + "897455\\f",
      UserSettingHelper.UserId
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm-update-password-token", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Token confirmation failed", $"{jsonObject["statusMessage"]}");


    var resetPasswordDTO = jsonObject["tokenPasswordResult"]?.ToObject<ConfirmUpdatePasswordDTO>();

    Assert.NotNull(resetPasswordDTO);
    Assert.False(resetPasswordDTO?.UpdatePasswordTokenConfirmed);
  }

  [Fact]
  public async Task Confirm_Password_Token_With_Non_Existent_Token_Does_Not_Confirm_Password_Token()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      ConfirmPasswordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetPasswordToken + "897455989")),
      UserSettingHelper.UserId
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm-update-password-token", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Token confirmation failed", $"{jsonObject["statusMessage"]}");


    var resetPasswordDTO = jsonObject["tokenPasswordResult"]?.ToObject<ConfirmUpdatePasswordDTO>();

    Assert.NotNull(resetPasswordDTO);
    Assert.False(resetPasswordDTO?.UpdatePasswordTokenConfirmed);
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

    Assert.Equal(3, errorList?.Count);

    Assert.True(errorList?.Any(x => x.Field == "NewPassword" && x.Message == "The NewPassword field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "UserId" && x.Message == "The UserId field is required.") ?? false);
    Assert.True(errorList?.Any(x => x.Field == "PasswordResetToken" && x.Message == "The PasswordResetToken field is required.") ?? false);
  }

  [Fact]
  public async Task Update_Password_For_Non_Existent_User_Does_Not_Update_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      NewPassword = "12345",
      UserId = Guid.NewGuid().ToString(),
      PasswordResetToken = resetPasswordToken
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

  [Fact]
  public async Task Update_Password_With_Invalid_Payload_Does_Not_Update_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);


    var requestPayload = new
    {
      NewPassword = "12345",
      UserSettingHelper.UserId,
      PasswordResetToken = resetPasswordToken
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

  [Fact]
  public async Task Update_Password_With_Valid_Payload_Updates_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);


    var requestPayload = new
    {
      NewPassword = "12345P@ss",
      UserSettingHelper.UserId,
      PasswordResetToken = resetPasswordToken
    };

    // Act
    var response = await _httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

    // Assert
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    Assert.NotNull(jsonObject);

    Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
    Assert.Equal("Password updated successfully", $"{jsonObject["statusMessage"]}");


    var updatePasswordDTO = jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDTO>();

    Assert.NotNull(updatePasswordDTO);
    Assert.True(updatePasswordDTO?.PassWordUpdated);
  }

  [Fact]
  public async Task Update_Password_With_Invalid_Token_Does_Not_Update_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      NewPassword = "12345",
      UserSettingHelper.UserId,
      PasswordResetToken = resetPasswordToken + "897455\\f",
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

  [Fact]
  public async Task Update_Password_With_Non_Existent_Token_Does_Not_Update_Password()
  {
    // Arrange
    _testDbHelper.SeedDatabase();
    var resetPasswordToken = _testDbHelper.UpdateResetPasswordToken(UserSettingHelper.UserId);

    var requestPayload = new
    {
      NewPassword = "12345",
      UserSettingHelper.UserId,
      PasswordResetToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(StringUtil.GenerateRandomString(56))),
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
