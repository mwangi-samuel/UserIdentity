using System.Data.Common;
using System.Net;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Presentation.Settings;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.Infrastructure.ExternalServices;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private static readonly string _baseUri = "/api/v1/user";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    private readonly StubHttpServer _stubHttpServer = testingWebAppFactory.StubHttpServer;

    [Fact]
    public async Task Get_Existing_User_Gets_User_Details()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item fetched successfully", $"{jsonObject["statusMessage"]}");

        var userDetails = jsonObject["user"]?.ToObject<UserDto>();

        Assert.Equal(UserSettingHelper.FirstName, userDetails?.FirstName);
        Assert.Equal(UserSettingHelper.UserName, userDetails?.UserName);
        Assert.Equal(UserSettingHelper.UserEmail, userDetails?.Email);
        Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
        Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);
    }

    [Fact]
    public async Task Get_User_Returns_The_Profile_Fields_And_No_Roles()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);

        // Assert
        var user = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["user"] as JObject;

        Assert.NotNull(user);
        Assert.Equal(
            ["createdAt", "createdBy", "email", "firstName", "id", "lastName", "phoneNumber", "updatedAt", "updatedBy", "userName"],
            user.Properties().Select(property => property.Name).Order()
        );
        Assert.Equal(UserSettingHelper.UserId, $"{user["id"]}");
        Assert.Equal(UserSettingHelper.PhoneNumber, $"{user["phoneNumber"]}");
    }

    [Fact]
    public async Task Get_User_Runs_One_Sql_Command()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestId = Guid.NewGuid().ToString("N");
        var request = await HttpClient.CreateAuthorizedHttpRequestMessageAsync(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);
        request.Headers.Add("X-Request-Id", requestId);

        // Act
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _testingWebAppFactory.SqlCommandCounter.CountFor(requestId));
    }

    [Fact]
    public async Task Login_Runs_Five_Sql_Commands()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestId = Guid.NewGuid().ToString("N");
        var request = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/login");
        request.Headers.Add("X-Request-Id", requestId);
        request.Content = SerDeHelper.ConvertToHttpContent(new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword });

        // Act
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, _testingWebAppFactory.SqlCommandCounter.CountFor(requestId));
    }

    [Fact]
    public async Task Refresh_Token_Runs_Three_Sql_Commands()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestId = Guid.NewGuid().ToString("N");
        var request = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
        request.Headers.Add("X-Request-Id", requestId);
        request.Content = SerDeHelper.ConvertToHttpContent(new { AccessToken = userToken, RefreshToken = refreshToken });

        // Act
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, _testingWebAppFactory.SqlCommandCounter.CountFor(requestId));
    }

    [Fact]
    public async Task Get_Non_Existing_User_Returns_Forbidden_Error_For_Unauthorized_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var nonExistingUserId = Guid.NewGuid().ToString();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Forbidden", $"{jsonObject["error"]?["message"]}");
    }

    [Fact]
    public async Task Get_Non_Existing_User_Does_Not_Get_User_Details_For_Authorized_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var nonExistingUserId = Guid.NewGuid().ToString();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {nonExistingUserId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Get_Deleted_AppUser_Does_Not_Get_User_Details_For_UnAuthorized_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        TestDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Get, _baseUri + "/" + UserSettingHelper.UserId);
        httpRequest.AddAuthHeader(userToken);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {UserSettingHelper.UserId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Get_Deleted_AppUser_Does_Not_Get_User_Details_For_Authorized_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateIdentityRole(TestDbHelper.AdminRoles.First());
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        (var userToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        TestDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

        var nonExistingUserId = Guid.NewGuid().ToString();

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Get, _baseUri + "/" + nonExistingUserId);
        httpRequest.AddAuthHeader(userToken);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("404 - NOT FOUND", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"No record exists for the provided identifier - {nonExistingUserId}", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Register_User_With_Invalid_Request_Payload_Returns_Validation_Errors()
    {
        // Arrange
        var requestPayload = new
        {
            UserSettingHelper.LastName,
            UserSettingHelper.PhoneNumber,
            UserSettingHelper.UserEmail,
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

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
            UserSettingHelper.UserPassword,
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

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

        Assert.True(
            errorList?.Any(x => x.Field == "UserEmail" && x.Message == "Either PhoneNumber or UserEmail must be provided.") ?? false
        );
        Assert.True(
            errorList?.Any(x => x.Field == "PhoneNumber" && x.Message == "Either PhoneNumber or UserEmail must be provided.") ?? false
        );
    }

    [Fact]
    public async Task Register_User_With_Valid_Request_Payload_Registers_User()
    {
        // Arrange
        var googleRecaptchaToken = RandomStrings.Generate(90);

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.LastName,
            UserSettingHelper.UserName,
            UserSettingHelper.PhoneNumber,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var userDetails = jsonObject["user"]?.ToObject<UserDto>();

        Assert.Equal(requestPayload.FirstName, userDetails?.FirstName);
        Assert.Equal(requestPayload.UserName, userDetails?.UserName);
        Assert.Equal(requestPayload.UserEmail, userDetails?.Email);
        Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
        Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

        var userToken = jsonObject["userToken"]?.ToObject<UserTokenDto>();

        Assert.Null(userToken);

        Assert.False(jsonObject["isConfirmed"]?.Value<bool>() ?? false);

        VerifyGoogleRecaptchaEndpoint();

        // Confirm the message reached the stream
        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userDetails!.Id);

        Assert.NotNull(message);
        Assert.Equal(RegisteredApp.AppName, message.RegisteredApp);
        Assert.Equal(MessageAction.WelcomeUser, message.Action);

        var messageAttributes = message.Attributes;
        Assert.NotNull(messageAttributes);

        Assert.Equal(requestPayload.UserEmail, messageAttributes.GetAttributeValue(MessageAttributeKey.RecipientEmail));
        Assert.Equal(requestPayload.PhoneNumber, messageAttributes.GetAttributeValue(MessageAttributeKey.RecipientPhone));
        Assert.Equal(requestPayload.FirstName, messageAttributes.GetAttributeValue(MessageAttributeKey.FirstName));
        Assert.Equal(requestPayload.LastName, messageAttributes.GetAttributeValue(MessageAttributeKey.LastName));
        Assert.Equal(requestPayload.UserName, messageAttributes.GetAttributeValue(MessageAttributeKey.UserName));
        Assert.Equal(userDetails?.Id, messageAttributes.GetAttributeValue(MessageAttributeKey.UserId));
    }

    [Theory]
    [MemberData(nameof(OverLongFields))]
    public async Task Register_User_With_Field_Over_Its_Limit_Returns_Validation_Error(string field, string value)
    {
        // Arrange
        var fields = GetSignUpFields();
        fields[field] = value;

        // Act
        var response = await RegisterAsync(fields);

        // Assert
        await AssertSignUpRefusedAsync(response, field);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    [InlineData("a b@c")]
    [InlineData("x@y\r\nBcc: z@y")]
    [InlineData("x\r\nBcc:y@z")]
    public async Task Register_User_With_Malformed_Email_Returns_Validation_Error(string userEmail)
    {
        // Arrange
        var fields = GetSignUpFields();
        fields["UserEmail"] = userEmail;

        // Act
        var response = await RegisterAsync(fields);

        // Assert
        await AssertSignUpRefusedAsync(response, "UserEmail");
    }

    [Fact]
    public async Task Register_User_With_Another_Users_Email_As_User_Name_Leaves_That_User_Alone()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var fields = GetSignUpFields();
        fields["UserName"] = UserSettingHelper.UserEmail;
        fields["UserEmail"] = "someone.else@example.com";

        // Act
        var response = await RegisterAsync(fields);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, TestDbHelper.CountIdentityUsers());

        await HttpClient.LoginUserAsync(UserSettingHelper.UserEmail, UserSettingHelper.UserPassword);

        var resetResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/reset-password",
            new { UserName = UserSettingHelper.UserEmail }
        );
        var resetRequestId = SerDeHelper
            .Deserialize<JObject>(await resetResponse.ValidateRequestResponseAsync())
            ?["resetPasswordDetails"]?["resetRequestId"]?.ToString();

        Assert.NotNull(resetRequestId);

        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.PasswordResetRequestedSubject, resetRequestId);

        Assert.Equal(UserSettingHelper.UserEmail, message?.Attributes.GetAttributeValue(MessageAttributeKey.RecipientEmail));
    }

    [Fact]
    public async Task Register_User_With_Own_Email_As_User_Name_In_Another_Case_Registers_User()
    {
        // Arrange
        var fields = GetSignUpFields();
        fields["UserName"] = "New.User@Example.com";
        fields["UserEmail"] = "new.user@example.com";

        // Act
        var response = await RegisterAsync(fields);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Two_Phone_Only_Users_With_Blank_Email_Registers_Both()
    {
        // Arrange
        var firstFields = GetSignUpFields();
        firstFields["UserName"] = "first.phone.user";
        firstFields["UserEmail"] = "";
        firstFields["PhoneNumber"] = "712121212";

        var secondFields = GetSignUpFields();
        secondFields["UserName"] = "second.phone.user";
        secondFields["UserEmail"] = "";
        secondFields["PhoneNumber"] = "712121213";

        // Act
        var firstResponse = await RegisterAsync(firstFields);
        var secondResponse = await RegisterAsync(secondFields);

        // Assert
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(2, TestDbHelper.CountIdentityUsers());

        var secondUser = SerDeHelper
            .Deserialize<JObject>(await secondResponse.ValidateRequestResponseAsync())
            ?["user"]?.ToObject<UserDto>();

        Assert.Null(secondUser?.Email);
    }

    [Fact]
    public async Task Register_User_Message_Leaves_The_Outbox_Once_It_Is_In_The_Stream()
    {
        // Arrange
        var googleRecaptchaToken = RandomStrings.Generate(90);

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.UserName,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var userId = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["user"]?["id"]?.ToString();

        Assert.NotNull(userId);
        Assert.NotNull(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userId));

        await Eventually.HoldsAsync(() => TestDbHelper.CountOutboxMessages() == 0);
    }

    [Theory]
    [InlineData("random@test.com", null)]
    [InlineData("random@test.com", "")]
    [InlineData(null, "712121212")]
    [InlineData("", "712121212")]
    public async Task Register_User_With_Only_Required_Request_Payload_Registers_User(string? UserEmail, string? PhoneNumber)
    {
        // Arrange
        var googleRecaptchaToken = RandomStrings.Generate(90);

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.UserName,
            UserSettingHelper.UserPassword,
            UserEmail,
            PhoneNumber,
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Item created successfully", $"{jsonObject["statusMessage"]}");

        var userDetails = jsonObject["user"]?.ToObject<UserDto>();

        Assert.Equal(requestPayload.FirstName, userDetails?.FirstName);
        Assert.Equal(requestPayload.UserName, userDetails?.UserName);
        Assert.Equal(string.IsNullOrEmpty(UserEmail) ? null : UserEmail, userDetails?.Email);
        Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
        Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

        var userToken = jsonObject["userToken"]?.ToObject<UserTokenDto>();

        Assert.Null(userToken);

        Assert.False(jsonObject["isConfirmed"]?.Value<bool>() ?? false);

        VerifyGoogleRecaptchaEndpoint();

        // Confirm the message reached the stream
        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userDetails!.Id);

        Assert.NotNull(message);
        Assert.Equal(RegisteredApp.AppName, message.RegisteredApp);
        Assert.Equal(MessageAction.WelcomeUser, message.Action);

        var messageAttributes = message.Attributes;
        Assert.NotNull(messageAttributes);

        Assert.Equal(requestPayload.UserEmail ?? string.Empty, messageAttributes.GetAttributeValue(MessageAttributeKey.RecipientEmail));
        Assert.Equal(requestPayload.PhoneNumber ?? string.Empty, messageAttributes.GetAttributeValue(MessageAttributeKey.RecipientPhone));
        Assert.Equal(requestPayload.FirstName, messageAttributes.GetAttributeValue(MessageAttributeKey.FirstName));
        Assert.Equal(string.Empty, messageAttributes.GetAttributeValue(MessageAttributeKey.LastName));
        Assert.Equal(requestPayload.UserName, messageAttributes.GetAttributeValue(MessageAttributeKey.UserName));
        Assert.Equal(userDetails?.Id, messageAttributes.GetAttributeValue(MessageAttributeKey.UserId));
    }

    [Fact]
    public async Task Register_Existing_User_With_Valid_Request_Payload_Does_Not_Create_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var googleRecaptchaToken = RandomStrings.Generate(90);

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.LastName,
            UserSettingHelper.UserName,
            UserSettingHelper.PhoneNumber,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");

        Assert.Equal($"A record identified with - {requestPayload.UserName} - exists", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Register_User_With_Invite_Code_Grants_The_Invites_Role_And_Its_Scopes()
    {
        // Arrange
        const string ownerRole = "testapp:owner";
        const string ownerScope = "catalog-product:all";
        var googleRecaptchaToken = RandomStrings.Generate(90);
        var userEmail = "invited-owner@example.com";
        var userName = "invited.owner";

        TestDbHelper.CreateIdentityRole(ownerRole);
        TestDbHelper.UpdateRoleClaim(ownerRole, ownerScope);
        var inviteCode = TestDbHelper.CreateInviteCodeEntity(userEmail, RegisteredApp.AppName, inviteCode: "OWNERINVITE", role: ownerRole);

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserName = userName,
            UserEmail = userEmail,
            UserSettingHelper.UserPassword,
            InviteCode = "ownerinvite",
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var userDetails = SerDeHelper.Deserialize<JObject>(responseString)?["user"]?.ToObject<UserDto>();

        Assert.NotNull(userDetails);
        Assert.NotNull(TestDbHelper.GetIdentityUserRole(ownerRole, userDetails.Id));
        Assert.True(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id)?.Applied);

        // Simulate the code being verified for the welcome message
        var attributes = new Dictionary<MessageAttributeKey, string>() { { MessageAttributeKey.UserId, userDetails.Id } };

        var messageEvent = MessageEventFactory.Create(
            userDetails.Id,
            RegisteredApp.AppName,
            attributes,
            MessageAction.WelcomeUser,
            CrudEvent.Update
        );

        await NatsTestHelper.PublishCodeVerifiedAsync(messageEvent);

        await Eventually.HoldsAsync(() => TestDbHelper.GetIdentityUserWithNoTracking(userDetails.Id)?.EmailConfirmed == true);

        (var token, _) = await HttpClient.LoginUserAsync(userName, UserSettingHelper.UserPassword);

        var claims = new JsonWebToken(token).Claims.ToList();

        Assert.Contains(claims, c => c.Type == JwtCustomClaimNames.Roles && c.Value == ownerRole);
        Assert.Contains(claims, c => c.Type == JwtCustomClaimNames.Scope && c.Value == ownerScope);
    }

    [Fact]
    public async Task Register_User_With_Invalid_Optional_Invite_Code_Does_Not_Create_User()
    {
        // Arrange
        var googleRecaptchaToken = RandomStrings.Generate(90);
        var userEmail = "wrong-invite@example.com";
        var userName = "wrong.invite";

        var inviteCode = TestDbHelper.CreateInviteCodeEntity(
            userEmail,
            RegisteredApp.AppName,
            inviteCode: "RIGHTCODE",
            role: "testapp:owner"
        );

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserName = userName,
            UserEmail = userEmail,
            UserSettingHelper.UserPassword,
            InviteCode = "WRONGCODE",
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id)?.Applied);

        var loginResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            ApiRequestHelper.loginUrl,
            new { UserName = userName, Password = UserSettingHelper.UserPassword }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_Existing_User_Logs_In_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Login successful", $"{jsonObject["statusMessage"]}");

        var userDetails = jsonObject["user"]?.ToObject<UserDto>();

        Assert.Equal(UserSettingHelper.FirstName, userDetails?.FirstName);
        Assert.Equal(requestPayload.UserName, userDetails?.UserName);
        Assert.Equal(UserSettingHelper.UserEmail, userDetails?.Email);
        Assert.Equal(userDetails?.Id, userDetails?.CreatedBy);
        Assert.Equal(userDetails?.Id, userDetails?.UpdatedBy);

        var userToken = jsonObject["userToken"]?.ToObject<UserTokenDto>();

        Assert.NotNull(userToken);
        Assert.IsType<AccessTokenDto>(userToken.AccessToken);
    }

    [Fact]
    public async Task Login_With_A_Body_That_Isnt_Json_Returns_An_Unreadable_Request_Error()
    {
        // Arrange
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/login");
        httpRequest.Content = new StringContent("{ not json", Encoding.UTF8, MediaTypeNames.Application.Json);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");
        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.NotNull(errorList);
        var error = Assert.Single(errorList);
        Assert.Null(error.Field);
        Assert.Equal("The request couldn't be read.", error.Message);
    }

    [Fact]
    public async Task Login_Non_Existing_User_Does_Not_Log_In_User()
    {
        // Arrange
        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Login_Existing_Non_Existent_AppUser_Does_Not_Log_In_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Login_Existing_Deleted_AppUser_Does_Not_Log_In_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.DeleteUserEntity(UserSettingHelper.UserId);

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Login_Existing_With_Invalid_Password_Does_Not_Log_In_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword + "123" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Login_Ticket_From_Sign_Up_Starts_A_Session_Once_The_User_Is_Confirmed_And_Only_Once()
    {
        // Arrange
        var googleRecaptchaToken = RandomStrings.Generate(90);
        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.UserName,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
            googleRecaptchaToken,
        };

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        var registerResponse = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);
        var registered = SerDeHelper.Deserialize<JObject>(await registerResponse.ValidateRequestResponseAsync());
        var userId = $"{registered?["user"]?["id"]}";
        var ticketPayload = new { Ticket = $"{registered?["loginTicket"]}" };

        // Act
        var beforeConfirming = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login-ticket", ticketPayload);

        await NatsTestHelper.PublishCodeVerifiedAsync(
            MessageEventFactory.Create(
                userId,
                RegisteredApp.AppName,
                new Dictionary<MessageAttributeKey, string> { { MessageAttributeKey.UserId, userId } },
                MessageAction.WelcomeUser,
                CrudEvent.Update
            )
        );
        await Eventually.HoldsAsync(() => TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed == true);

        var afterConfirming = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login-ticket", ticketPayload);
        var reused = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login-ticket", ticketPayload);

        // Assert
        Assert.False(string.IsNullOrEmpty(ticketPayload.Ticket));
        Assert.Equal(HttpStatusCode.NotAcceptable, beforeConfirming.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterConfirming.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        var session = SerDeHelper.Deserialize<JObject>(await afterConfirming.ValidateRequestResponseAsync());
        var accessToken = $"{session?["userToken"]?["accessToken"]?["token"]}";

        Assert.Equal(userId, new JsonWebToken(accessToken).Subject);
        Assert.False(string.IsNullOrEmpty($"{session?["userToken"]?["refreshToken"]}"));
    }

    [Fact]
    public async Task Login_Ticket_Is_Refused_For_Another_App()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        var loginResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/login",
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );
        var ticket = $"{SerDeHelper.Deserialize<JObject>(await loginResponse.ValidateRequestResponseAsync())?["loginTicket"]}";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _baseUri + "/login-ticket")
        {
            Content = SerDeHelper.ConvertToHttpContent(new { Ticket = ticket }),
        };
        httpRequest.AddXApiKey();
        httpRequest.Headers.Add("X-App-Name", TestingWebAppFactory.OtherAppName);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(string.IsNullOrEmpty(ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_Valid_User_Token_Refreshes_User_token()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestPayload = new { AccessToken = userToken, RefreshToken = refreshToken };

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();
        OutputHelper.WriteLine(responseString);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Refresh token generated successfully", $"{jsonObject["statusMessage"]}");

        var userTokenVM = jsonObject["userToken"]?.ToObject<UserTokenDto>();

        Assert.NotNull(userTokenVM);
        Assert.IsType<AccessTokenDto>(userTokenVM.AccessToken);

        Assert.NotEqual(userToken, userTokenVM.AccessToken.Token);
        Assert.NotEqual(refreshToken, userTokenVM.RefreshToken);

        Assert.True(userTokenVM?.AccessToken.ExpiresIn > 0);
    }

    [Fact]
    public async Task Refresh_User_Token_With_Invalid_Refresh_Token_Does_Not_Refresh_User_token()
    {
        // Arrange// Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestPayload = new { AccessToken = userToken, RefreshToken = Base64UrlEncoder.Encode(refreshToken) };

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("An invalid access token was provided", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Refresh_User_Token_With_Deleted_Refresh_Token_Does_Not_Refresh_User_token()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        TestDbHelper.DeleteRefreshTokenEntity(UserSettingHelper.UserId);

        var requestPayload = new { AccessToken = userToken, RefreshToken = Base64UrlEncoder.Encode(refreshToken) };

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("An invalid access token was provided", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Refresh_User_Token_With_Invalid_Access_Token_Does_Not_Refresh_User_token()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (_, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestPayload = new { AccessToken = UserSettingHelper.InvalidUserToken, RefreshToken = refreshToken };

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/refresh-token");
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("401 - UNAUTHORIZED", $"{jsonObject["statusMessage"]}");

        Assert.Equal("An invalid access token was provided", $"{jsonObject["error"]?["message"]}");

        AssertErrorTimestamp(jsonObject);
    }

    [Fact]
    public async Task Login_Stores_Only_The_Hash_Of_The_Refresh_Token()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        (_, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        // Assert
        var refreshTokenEntities = TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId);

        Assert.DoesNotContain(refreshTokenEntities, r => r.Token == refreshToken);
        Assert.Contains(
            refreshTokenEntities,
            r => r.Token == new TokenFactory().HashToken(refreshToken) && r.Expires > DateTime.UtcNow.AddDays(29)
        );
    }

    [Fact]
    public async Task Login_After_Too_Many_Failed_Attempts_Locks_Out_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.LockoutEnabled = true;
        TestDbHelper.UpdateIdentityUser(user);

        var wrongPasswordPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword + "123" };

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        for (var i = 0; i < 5; i++)
            await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", wrongPasswordPayload);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");

        var lockedOutUser = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

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
        });

        using var httpClient = rateLimitedFactory.CreateClient();

        var requestPayload = new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

        // Act
        var allowedResponses = new List<HttpResponseMessage>
        {
            await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload),
            await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/login", requestPayload),
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
        Assert.Equal("Too many requests, try again later", $"{jsonObject["error"]?["message"]}");
    }

    [Theory]
    [InlineData("/api/v1/user/register")]
    [InlineData("/api/v1/wait-list")]
    [InlineData("/api/v1/user/reset-password")]
    public async Task Anonymous_Endpoint_Beyond_Rate_Limit_Returns_Too_Many_Requests(string uri)
    {
        // Arrange
        await using var rateLimitedFactory = _testingWebAppFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.Enabled)}", "true");
            builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.PermitLimit)}", "1");
            builder.UseSetting($"{nameof(RateLimitSettings)}:{nameof(RateLimitSettings.WindowSeconds)}", "60");
        });

        using var httpClient = rateLimitedFactory.CreateClient();

        var allowedResponse = await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, uri, new { });

        // Act
        var response = await httpClient.SendNoAuthRequestAsync(HttpMethod.Post, uri, new { });

        // Assert
        Assert.NotEqual(HttpStatusCode.TooManyRequests, allowedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Refresh_User_Token_With_Expired_Refresh_Token_Does_Not_Refresh_User_Token()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        TestDbHelper.ExpireRefreshTokenEntities(UserSettingHelper.UserId);

        var requestPayload = new { AccessToken = userToken, RefreshToken = refreshToken };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("An invalid access token was provided", $"{jsonObject["error"]?["message"]}");

        var exchangedTokenHash = new TokenFactory().HashToken(refreshToken);
        Assert.DoesNotContain(
            TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId),
            token => token.Token == exchangedTokenHash
        );
    }

    [Fact]
    public async Task Refresh_User_Token_With_Already_Used_Refresh_Token_Ends_Session()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var requestPayload = new { AccessToken = userToken, RefreshToken = refreshToken };

        var firstResponse = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var userTokenVM = SerDeHelper
            .Deserialize<JObject>(await firstResponse.ValidateRequestResponseAsync())
            ?["userToken"]?.ToObject<UserTokenDto>();

        Assert.NotNull(userTokenVM?.RefreshToken);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/refresh-token", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("An invalid access token was provided", $"{jsonObject["error"]?["message"]}");

        var newTokenResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/refresh-token",
            new { AccessToken = userTokenVM.AccessToken?.Token, userTokenVM.RefreshToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, newTokenResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_User_Ends_Session()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, _baseUri + "/logout");
        httpRequest.AddAuthHeader(userToken);
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(new { RefreshToken = refreshToken });

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Logout successful", $"{jsonObject["statusMessage"]}");

        var refreshResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/refresh-token",
            new { AccessToken = userToken, RefreshToken = refreshToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_User_Without_Access_Token_Returns_Unauthorized()
    {
        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/logout",
            new { RefreshToken = RandomStrings.Generate(32) }
        );

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reset_Password_With_Invalid_Payload_Returns_Validation_Results()
    {
        // Arrange
        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

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

        Assert.True(errorList?.Any(x => x.Field == "UserName" && x.Message == "The UserName field is required.") ?? false);
    }

    [Fact]
    public async Task Reset_Password_For_Existing_User_Creates_Reset_Request()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserName = UserSettingHelper.UserEmail };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Password reset request successful", $"{jsonObject["statusMessage"]}");

        var resetPasswordDto = jsonObject["resetPasswordDetails"]?.ToObject<ResetPasswordDto>();

        Assert.NotNull(resetPasswordDto?.ResetRequestId);

        var passwordResetEntity = TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId);

        Assert.NotNull(passwordResetEntity);
        Assert.Equal(new TokenFactory().HashToken(resetPasswordDto.ResetRequestId), passwordResetEntity.RequestId);
        Assert.False(passwordResetEntity.Verified);
        Assert.False(passwordResetEntity.Applied);
    }

    [Fact]
    public async Task Reset_Password_Requests_Sent_At_Once_Store_One_Request_And_Send_One_Message()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, 5)
                .Select(_ =>
                    HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", new { UserSettingHelper.UserName })
                )
        );

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var resetRequestIds = await Task.WhenAll(
            responses.Select(async response =>
                $"{SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["resetPasswordDetails"]?["resetRequestId"]}"
            )
        );
        var messages = await Task.WhenAll(
            resetRequestIds.Select(resetRequestId =>
                NatsTestHelper.FindMessageAsync(NatsTestHelper.PasswordResetRequestedSubject, resetRequestId)
            )
        );

        Assert.Equal(1, TestDbHelper.CountPasswordResetEntities(UserSettingHelper.UserId));
        Assert.Single(messages, message => message is not null);
    }

    [Fact]
    public async Task Reset_Password_For_Non_Existing_User_Returns_Same_Response_Without_Creating_Reset_Request()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { UserName = "hello@test.com" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Password reset request successful", $"{jsonObject["statusMessage"]}");

        var resetPasswordDto = jsonObject["resetPasswordDetails"]?.ToObject<ResetPasswordDto>();

        Assert.NotNull(resetPasswordDto?.ResetRequestId);
        Assert.Null(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Reset_Password_For_Non_Existing_UserDetails_Record_Does_Not_Create_Reset_Request()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();

        var requestPayload = new { UserSettingHelper.UserName };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/reset-password", requestPayload);

        // Assert
        await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Reset_Password_With_Verified_Code_Updates_Password_And_Ends_Sessions()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var resetResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/reset-password",
            new { UserSettingHelper.UserName }
        );
        var resetRequestId = SerDeHelper
            .Deserialize<JObject>(await resetResponse.ValidateRequestResponseAsync())
            ?["resetPasswordDetails"]?["resetRequestId"]?.ToString();

        Assert.NotNull(resetRequestId);

        // Simulate the message saying a code was verified
        var attributes = new Dictionary<MessageAttributeKey, string>() { { MessageAttributeKey.UserId, resetRequestId } };

        var messageEvent = MessageEventFactory.Create(
            resetRequestId,
            RegisteredApp.AppName,
            attributes,
            MessageAction.PasswordReset,
            CrudEvent.Update
        );

        await NatsTestHelper.PublishCodeVerifiedAsync(messageEvent);

        await Eventually.HoldsAsync(() => TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Verified == true);

        var lockedOutUser = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        lockedOutUser.LockoutEnabled = true;
        lockedOutUser.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        lockedOutUser.AccessFailedCount = 3;
        TestDbHelper.UpdateIdentityUser(lockedOutUser);

        var newPassword = "N3wP@ssword!";

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new { ResetRequestId = resetRequestId, NewPassword = newPassword }
        );

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Successful", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Password updated successfully", $"{jsonObject["statusMessage"]}");
        Assert.True(jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDto>()?.PasswordUpdated);

        Assert.True(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);

        var updatedUser = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

        Assert.Null(updatedUser?.LockoutEnd);
        Assert.Equal(0, updatedUser?.AccessFailedCount);

        await HttpClient.LoginUserAsync(UserSettingHelper.UserName, newPassword);

        var refreshResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/refresh-token",
            new { AccessToken = userToken, RefreshToken = refreshToken }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);

        var reuseResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new { ResetRequestId = resetRequestId, NewPassword = "An0therP@ssword!" }
        );

        Assert.Equal(HttpStatusCode.NotAcceptable, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Update_Password_That_Fails_Part_Way_Leaves_Password_Sessions_And_Reset_Request_As_They_Were()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = RandomStrings.Generate(32);
        TestDbHelper.CreatePasswordResetEntity(resetRequestId);

        var originalPasswordHash = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.PasswordHash;

        await using var failingFactory = _testingWebAppFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(new FailsDeletingSessionsInterceptor()))
            )
        );

        using var failingClient = failingFactory.CreateClient();

        // Act
        var response = await failingClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new { ResetRequestId = resetRequestId, NewPassword = "N3wP@ssword!" }
        );

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(originalPasswordHash, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.PasswordHash);
        Assert.False(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
        Assert.Single(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Update_Password_With_Invalid_Payload_Returns_Validation_Results()
    {
        // Arrange
        var requestPayload = new { };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

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

        Assert.True(errorList?.Any(x => x.Field == "NewPassword" && x.Message == "The NewPassword field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "ResetRequestId" && x.Message == "The ResetRequestId field is required.") ?? false);
    }

    [Fact]
    public async Task Update_Password_With_Unknown_Reset_Request_Does_Not_Update_Password()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new { ResetRequestId = RandomStrings.Generate(32), NewPassword = "N3wP@ssword!" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("Password update failed", $"{jsonObject["statusMessage"]}");

        var updatePasswordDto = jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDto>();

        Assert.NotNull(updatePasswordDto);
        Assert.False(updatePasswordDto?.PasswordUpdated);
    }

    [Theory]
    [InlineData(false, false, 15)]
    [InlineData(true, true, 15)]
    [InlineData(true, false, -1)]
    public async Task Update_Password_With_Unverified_Used_Or_Expired_Reset_Request_Does_Not_Update_Password(
        bool verified,
        bool applied,
        int expiresInMinutes
    )
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = RandomStrings.Generate(32);
        TestDbHelper.CreatePasswordResetEntity(resetRequestId, verified, applied, DateTime.UtcNow.AddMinutes(expiresInMinutes));

        var requestPayload = new { ResetRequestId = resetRequestId, NewPassword = "N3wP@ssword!" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Password update failed", $"{jsonObject["statusMessage"]}");
        Assert.False(jsonObject["updatePasswordResult"]?.ToObject<UpdatePasswordDto>()?.PasswordUpdated);

        await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);
    }

    [Fact]
    public async Task Update_Password_With_Password_Not_Meeting_Rules_Returns_Validation_Errors()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = RandomStrings.Generate(32);
        TestDbHelper.CreatePasswordResetEntity(resetRequestId);

        var requestPayload = new { ResetRequestId = resetRequestId, NewPassword = "12345" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        Assert.Equal("Request Failed", $"{jsonObject["requestStatus"]}");
        Assert.Equal("400 - BAD REQUEST", $"{jsonObject["statusMessage"]}");
        Assert.Contains("Passwords must be at least 8 characters.", $"{jsonObject["error"]?["message"]}");

        Assert.False(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
    }

    private sealed class FailsDeletingSessionsInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        ) =>
            command.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("refresh_tokens", StringComparison.Ordinal)
                ? throw new InvalidOperationException("Failed part way")
                : ValueTask.FromResult(result);
    }

    public static TheoryData<string, string> OverLongFields =>
        new()
        {
            { "FirstName", new string('a', UserLimits.FirstNameMaxLength + 1) },
            { "LastName", new string('a', UserLimits.LastNameMaxLength + 1) },
            { "UserName", new string('a', UserLimits.UserNameMaxLength + 1) },
            { "PhoneNumber", new string('1', UserLimits.PhoneNumberMaxLength + 1) },
            { "PhoneNumber", new string('1', 1_200_000) },
            { "UserEmail", new string('a', UserLimits.EmailMaxLength) + "@example.com" },
            { "InviteCode", new string('A', InviteCodeLimits.InviteCodeMaxLength + 1) },
        };

    private static Dictionary<string, string?> GetSignUpFields() =>
        new()
        {
            ["FirstName"] = UserSettingHelper.FirstName,
            ["UserName"] = UserSettingHelper.UserName,
            ["UserEmail"] = UserSettingHelper.UserEmail,
            ["UserPassword"] = UserSettingHelper.UserPassword,
        };

    private async Task<HttpResponseMessage> RegisterAsync(Dictionary<string, string?> fields)
    {
        var googleRecaptchaToken = RandomStrings.Generate(90);

        SetUpGoogleRecaptchaEndpoint(googleRecaptchaToken);

        return await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/register",
            new Dictionary<string, string?>(fields) { ["GoogleRecaptchaToken"] = googleRecaptchaToken }
        );
    }

    private async Task AssertSignUpRefusedAsync(HttpResponseMessage response, string field)
    {
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("Validation Failed", $"{jsonObject["error"]?["message"]}");

        var errorList = jsonObject["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.True(errorList?.Any(x => x.Field == field) ?? false);
        Assert.Equal(0, TestDbHelper.CountIdentityUsers());
        Assert.Equal(0, TestDbHelper.CountOutboxMessages());
    }

    private void SetUpGoogleRecaptchaEndpoint(string googleRecaptchaToken)
    {
        var googleRecaptchaSettings = Configuration.GetSetting<GoogleRecaptchaSettings>();

        _stubHttpServer.Reset();
        _stubHttpServer.OnPost(
            "/siteverify",
            request =>
                request.Query.GetValueOrDefault("secret") == googleRecaptchaSettings.SiteKey
                && request.Query.GetValueOrDefault("response") == googleRecaptchaToken
                    ? Results.Json(new { success = true, score = 0.9f })
                    : Results.NotFound()
        );
    }

    private void VerifyGoogleRecaptchaEndpoint() => Assert.Contains(_stubHttpServer.Requests, request => request.Path == "/siteverify");
}
