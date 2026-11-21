using System.Data.Common;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.RegisteredApps.Commands;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Presentation.Settings;
using PolyzenKit.Presentation.Validation;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private static readonly string _baseUri = "/api/v1/user";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

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
    public async Task Login_Runs_Seven_Sql_Commands()
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
        Assert.Equal(6, _testingWebAppFactory.SqlCommandCounter.CountFor(requestId));
    }

    [Fact]
    public async Task Refresh_Token_Runs_Eight_Sql_Commands()
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
        Assert.Equal(7, _testingWebAppFactory.SqlCommandCounter.CountFor(requestId));
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
        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.LastName,
            UserSettingHelper.UserName,
            UserSettingHelper.PhoneNumber,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
        };

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

        Assert.Null(jsonObject["userToken"]?.ToObject<UserTokenDto>());
        Assert.Equal("ConfirmEmail", $"{jsonObject["nextStep"]}");
        Assert.False(string.IsNullOrEmpty($"{jsonObject["ticket"]}"));
        Assert.Equal("t***@domain.com", $"{jsonObject["codeSentTo"]}");

        Assert.Contains(CaptchaVerifier.Checks, check => check.Action == "register");

        var loginTicket = Assert.Single(TestDbHelper.GetLoginTicketEntitiesWithNoTracking(userDetails!.Id));

        Assert.Equal(SignInStep.ConfirmEmail, loginTicket.Step);
        Assert.Equal(UserMessages.WelcomeUserAction, loginTicket.CodeAction);
        Assert.Equal(userDetails.Id, loginTicket.CodeCorrelationId);
        Assert.True(loginTicket.Expires > DateTime.UtcNow.AddMinutes(50));

        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userDetails.Id);

        Assert.NotNull(message);
        Assert.Equal(RegisteredApp.AppName, message.RegisteredApp);
        Assert.Equal(UserMessages.WelcomeUserAction, message.Action);

        Assert.Equal(
            new MessageRecipient
            {
                Email = requestPayload.UserEmail,
                Phone = requestPayload.PhoneNumber,
                Name = $"{requestPayload.FirstName} {requestPayload.LastName}",
            },
            message.Recipient
        );

        var data = message.GetData<UserMessageData>();

        Assert.Equal(requestPayload.FirstName, data?.FirstName);
        Assert.NotNull(data?.RequestedAt);
        Assert.False(message.Data.TryGetProperty("userId", out _));
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

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var passwordReset = TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId);

        Assert.NotNull(passwordReset);

        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.PasswordResetRequestedSubject, passwordReset.Id.ToString());

        Assert.Equal(UserSettingHelper.UserEmail, message?.Recipient?.Email);
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
        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.UserName,
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
        };

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
        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserSettingHelper.UserName,
            UserSettingHelper.UserPassword,
            UserEmail,
            PhoneNumber,
        };

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

        Assert.Null(jsonObject["userToken"]?.ToObject<UserTokenDto>());
        Assert.Equal("ConfirmEmail", $"{jsonObject["nextStep"]}");
        Assert.Equal(string.IsNullOrEmpty(UserEmail) ? "***212" : "r***@test.com", $"{jsonObject["codeSentTo"]}");

        Assert.Contains(CaptchaVerifier.Checks, check => check.Action == "register");

        var message = await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userDetails!.Id);

        Assert.NotNull(message);
        Assert.Equal(RegisteredApp.AppName, message.RegisteredApp);
        Assert.Equal(UserMessages.WelcomeUserAction, message.Action);

        Assert.Equal(
            new MessageRecipient
            {
                Email = string.IsNullOrEmpty(UserEmail) ? null : UserEmail,
                Phone = string.IsNullOrEmpty(PhoneNumber) ? null : PhoneNumber,
                Name = requestPayload.FirstName,
            },
            message.Recipient
        );
        Assert.Equal(requestPayload.FirstName, message.GetData<UserMessageData>()?.FirstName);
        Assert.False(message.Data.TryGetProperty("lastName", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_With_A_Taken_User_Name_Or_Email_Answers_Like_A_New_Sign_Up_And_Tells_The_Account_Owner(bool sameUserName)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserName = sameUserName ? UserSettingHelper.UserName : "someone.else",
            UserEmail = sameUserName ? "someone.else@example.com" : UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());
        var userId = $"{jsonObject?["user"]?["id"]}";
        var ticket = $"{jsonObject?["ticket"]}";

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("ConfirmEmail", $"{jsonObject?["nextStep"]}");
        Assert.Equal(ContactMask.Email(requestPayload.UserEmail), $"{jsonObject?["codeSentTo"]}");
        Assert.True(Guid.TryParse(userId, out _));
        Assert.NotEqual(UserSettingHelper.UserId, userId);
        Assert.False(string.IsNullOrEmpty(ticket));
        Assert.Equal(requestPayload.UserName, $"{jsonObject?["user"]?["userName"]}");
        Assert.Equal(1, TestDbHelper.CountIdentityUsers());
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));
        Assert.Equal(1, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.SignUpAttemptNotice));

        var attempt = await FindMessageSentToAsync(NatsTestHelper.SignUpAttemptedSubject, UserSettingHelper.UserEmail);

        Assert.NotNull(attempt);
        Assert.Equal(UserMessages.SignUpAttemptedAction, attempt.Action);
        Assert.NotNull(attempt.GetData<UserMessageData>()?.AttemptedAt);
        Assert.Null(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userId, TimeSpan.FromSeconds(1)));

        StubHttpServer.AnswerVerify("Verified");
        StubHttpServer.AnswerRetry("Sent");

        var confirmResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new { Ticket = ticket, Code = "123456" }
        );
        var resendResponse = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/resend-code", new { Ticket = ticket });

        await AssertCodeErrorAsync(confirmResponse, ErrorMessages.Codes.NotAccepted);
        await AssertCodeResendRequestedAsync(resendResponse);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));
        var resend = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.RetryPath));

        Assert.NotEqual(UserSettingHelper.UserId, $"{check["correlationId"]}");
        Assert.NotEqual(userId, $"{check["correlationId"]}");
        Assert.NotEqual(userId, $"{resend["correlationId"]}");
    }

    [Fact]
    public async Task Register_With_A_Taken_Email_Twice_Within_The_Cooldown_Tells_The_Account_Owner_Once()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var requestPayload = new
        {
            UserSettingHelper.FirstName,
            UserName = "someone.else",
            UserSettingHelper.UserEmail,
            UserSettingHelper.UserPassword,
        };

        // Act
        var first = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);
        var second = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("ConfirmEmail", $"{SerDeHelper.Deserialize<JObject>(await second.ValidateRequestResponseAsync())?["nextStep"]}");
        Assert.Equal(1, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.SignUpAttemptNotice));
    }

    [Fact]
    public async Task Register_With_A_Taken_Email_Past_The_Daily_Notice_Cap_Answers_The_Same_And_Sends_Nothing()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.SignUpAttemptNotice, 5);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/register",
            new
            {
                UserSettingHelper.FirstName,
                UserName = "someone.else",
                UserSettingHelper.UserEmail,
                UserSettingHelper.UserPassword,
            }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("ConfirmEmail", $"{jsonObject?["nextStep"]}");
        Assert.Equal(5, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.SignUpAttemptNotice));
        Assert.Null(
            await FindMessageSentToAsync(NatsTestHelper.SignUpAttemptedSubject, UserSettingHelper.UserEmail, TimeSpan.FromSeconds(1))
        );
    }

    [Fact]
    public async Task Register_With_A_Taken_User_Name_Or_Email_For_An_App_Without_Confirmation_Gives_One_Generic_Error()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        const string openAppName = "OpenApp";

        var createRegisteredAppCommandHandler = ServiceProvider.GetRequiredService<
            ICreateItemCommandHandler<CreateRegisteredAppCommand, RegisteredAppViewModel>
        >();

        await createRegisteredAppCommandHandler.CreateItemAsync(
            new CreateRegisteredAppCommand { AppName = openAppName, BaseUrl = "http://localhost:3003" },
            KnownUsers.SystemId,
            TestContext.Current.CancellationToken
        );

        object[] payloads =
        [
            new
            {
                UserSettingHelper.FirstName,
                UserSettingHelper.UserName,
                UserEmail = "someone.else@example.com",
                UserSettingHelper.UserPassword,
            },
            new
            {
                UserSettingHelper.FirstName,
                UserName = "someone.else",
                UserSettingHelper.UserEmail,
                UserSettingHelper.UserPassword,
            },
        ];

        List<HttpResponseMessage> responses = [];

        // Act
        foreach (var payload in payloads)
            responses.Add(await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", payload, openAppName));

        // Assert
        foreach (var response in responses)
            await AssertCodeErrorAsync(response, ErrorMessages.Users.AccountNotCreated);

        Assert.Equal(1, TestDbHelper.CountIdentityUsers());
        Assert.Equal(0, TestDbHelper.CountOutboxMessages());
    }

    [Fact]
    public async Task Register_User_With_Invite_Code_Grants_The_Invites_Role_And_Its_Scopes()
    {
        // Arrange
        const string ownerRole = "testapp:owner";
        const string ownerScope = "catalog-product:all";
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
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", requestPayload);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var userDetails = SerDeHelper.Deserialize<JObject>(responseString)?["user"]?.ToObject<UserDto>();

        Assert.NotNull(userDetails);
        Assert.NotNull(TestDbHelper.GetIdentityUserRole(ownerRole, userDetails.Id));
        Assert.True(TestDbHelper.GetInviteCodeEntityWithNoTracking(inviteCode.Id)?.Applied);

        var invitedUser = TestDbHelper.GetIdentityUser(userDetails.Id)!;
        invitedUser.EmailConfirmed = true;
        TestDbHelper.UpdateIdentityUser(invitedUser);

        (var token, _) = await HttpClient.LoginUserAsync(userName, UserSettingHelper.UserPassword);

        var claims = new JsonWebToken(token).Claims.ToList();

        Assert.Contains(claims, c => c.Type == JwtCustomClaimNames.Roles && c.Value == ownerRole);
        Assert.Contains(claims, c => c.Type == JwtCustomClaimNames.Scope && c.Value == ownerScope);
    }

    [Fact]
    public async Task Register_User_With_Invalid_Optional_Invite_Code_Does_Not_Create_User()
    {
        // Arrange
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
        };

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
    public async Task Confirm_With_The_Right_Code_Confirms_The_Account_And_Starts_A_Session_Only_Once()
    {
        // Arrange
        (var userId, var ticket) = await SignUpUnconfirmedAsync();

        StubHttpServer.AnswerVerify("Verified");

        var confirmPayload = new { Ticket = ticket, Code = "123456" };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm", confirmPayload);
        var reused = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/confirm", confirmPayload);

        // Assert
        var session = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Account confirmed", $"{session?["statusMessage"]}");
        Assert.Equal("Session", $"{session?["nextStep"]}");
        Assert.Equal(userId, new JsonWebToken($"{session?["userToken"]?["accessToken"]?["token"]}").Subject);
        Assert.False(string.IsNullOrEmpty($"{session?["userToken"]?["refreshToken"]}"));
        Assert.True(TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed);
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(userId));

        var verifyRequest = StubHttpServer.Requests[0];
        var check = JsonNode.Parse(verifyRequest.Body)!;

        Assert.Equal(CodeCheckStub.VerifyPath, verifyRequest.Path);
        Assert.StartsWith("Bearer ", verifyRequest.Headers["Authorization"], StringComparison.Ordinal);
        Assert.Equal("TestApiKey-For-Local-Use-Only-0123456789", verifyRequest.Headers["X-Api-Key"]);
        Assert.Equal(UserMessages.WelcomeUserAction, $"{check["action"]}");
        Assert.Equal(userId, $"{check["correlationId"]}");
        Assert.Equal("123456", $"{check["code"]}");

        var serviceToken = new JsonWebToken(verifyRequest.Headers["Authorization"]["Bearer ".Length..]);

        Assert.Equal("useridentity", serviceToken.Subject);
        Assert.Equal("testapp", serviceToken.GetClaim(JwtRegisteredClaimNames.Azp).Value.ToLowerInvariant());
        Assert.Contains(serviceToken.Claims, claim => claim.Type == JwtCustomClaimNames.Roles && claim.Value == KnownRoles.InternalService);
        Assert.Contains(
            serviceToken.Claims,
            claim => claim.Type == JwtCustomClaimNames.Scope && claim.Value == "messagemanager-message:create"
        );

        await AssertCodeErrorAsync(reused, ErrorMessages.Codes.NotAccepted);
    }

    [Theory]
    [InlineData("WrongCode", 2)]
    [InlineData("Expired", 0)]
    [InlineData("NoAttemptsLeft", 0)]
    [InlineData("NotFound", 0)]
    public async Task Confirm_With_A_Code_That_Isnt_Verified_Gives_One_Message_And_Keeps_The_Account_Unconfirmed_And_The_Ticket(
        string outcome,
        int attemptsLeft
    )
    {
        // Arrange
        (var userId, var ticket) = await SignUpUnconfirmedAsync();

        StubHttpServer.AnswerVerify(outcome, attemptsLeft);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new { Ticket = ticket, Code = "000000" }
        );

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);
        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed);
        Assert.Equal(1, TestDbHelper.CountLoginTicketEntities(userId));
    }

    [Fact]
    public async Task Confirm_When_The_Code_Cant_Be_Checked_Fails_Closed()
    {
        // Arrange
        (var userId, var ticket) = await SignUpUnconfirmedAsync();

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new { Ticket = ticket, Code = "123456" }
        );

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed);
        Assert.Equal(1, TestDbHelper.CountLoginTicketEntities(userId));
    }

    [Fact]
    public async Task Confirm_With_A_Ticket_Of_Another_App_Is_Refused_And_Checks_No_Code_Of_The_User()
    {
        // Arrange
        (var userId, var ticket) = await SignUpUnconfirmedAsync();

        StubHttpServer.AnswerVerify("Verified");

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new { Ticket = ticket, Code = "123456" },
            TestingWebAppFactory.OtherAppName
        );

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.NotEqual(userId, $"{check["correlationId"]}");
        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed);
    }

    [Fact]
    public async Task Login_Of_An_Unconfirmed_User_Sends_A_Login_Code_And_Returns_A_Ticket_That_Confirm_Accepts()
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
        var login = SerDeHelper.Deserialize<JObject>(await loginResponse.ValidateRequestResponseAsync());
        var loginTicket = Assert.Single(TestDbHelper.GetLoginTicketEntitiesWithNoTracking(UserSettingHelper.UserId));

        StubHttpServer.AnswerVerify("Verified");

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new { Ticket = $"{login?["ticket"]}", Code = "123456" }
        );

        // Assert
        Assert.Equal("ConfirmEmail", $"{login?["nextStep"]}");
        Assert.Equal("t***@domain.com", $"{login?["codeSentTo"]}");
        Assert.True(string.IsNullOrEmpty($"{login?["userToken"]}"));
        Assert.Equal(UserMessages.LoginCodeAction, loginTicket.CodeAction);
        Assert.Equal(loginTicket.Id.ToString(), loginTicket.CodeCorrelationId);
        Assert.True(loginTicket.Expires < DateTime.UtcNow.AddMinutes(11));

        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, loginTicket.Id.ToString());

        Assert.NotNull(loginCode);
        Assert.Equal(UserMessages.LoginCodeAction, loginCode.Action);
        Assert.Equal(LoginCodePurpose.ConfirmEmail, loginCode.GetData<UserMessageData>()?.Purpose);
        Assert.Equal(UserSettingHelper.UserEmail, loginCode.Recipient?.Email);
        Assert.Null(loginCode.Recipient?.Phone);
        Assert.Equal(loginTicket.Expires, loginCode.ExpiresAt!.Value, TimeSpan.FromSeconds(1));

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.Equal(UserMessages.LoginCodeAction, $"{check["action"]}");
        Assert.Equal(loginTicket.Id.ToString(), $"{check["correlationId"]}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Session", $"{SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["nextStep"]}");
        Assert.True(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.EmailConfirmed);
    }

    [Fact]
    public async Task Login_Past_The_Daily_Login_Code_Cap_Answers_The_Lockout_And_Sends_No_Code()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent, 10);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/login",
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Provided credentials are invalid", $"{jsonObject?["error"]?["message"]}");
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));
        Assert.Equal(10, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent));
    }

    [Fact]
    public async Task Login_With_The_Right_Password_Keeps_The_Failure_Count_Until_A_Session_Starts()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.EmailConfirmed = false;
        user.LockoutEnabled = true;
        TestDbHelper.UpdateIdentityUser(user);

        await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/login",
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword + "123" }
        );

        // Act
        var confirmStep = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/login",
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );
        var countAfterConfirmStep = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount;

        StubHttpServer.AnswerVerify("Verified");

        var session = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/confirm",
            new
            {
                Ticket = $"{SerDeHelper.Deserialize<JObject>(await confirmStep.ValidateRequestResponseAsync())?["ticket"]}",
                Code = "123456",
            }
        );

        // Assert
        Assert.Equal(1, countAfterConfirmStep);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Equal(0, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount);
    }

    [Fact]
    public async Task Resend_Code_With_A_Sign_Up_Ticket_Asks_For_The_Welcome_Code_Of_Its_User()
    {
        // Arrange
        (var userId, var ticket) = await SignUpUnconfirmedAsync();

        StubHttpServer.AnswerRetry("Sent");

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/resend-code", new { Ticket = ticket });

        // Assert
        await AssertCodeResendRequestedAsync(response);

        var resend = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.RetryPath));
        Assert.Equal(UserMessages.WelcomeUserAction, $"{resend["action"]}");
        Assert.Equal(userId, $"{resend["correlationId"]}");
    }

    [Theory]
    [InlineData("TooSoon", 42)]
    [InlineData("NoResendsLeft", 0)]
    [InlineData("NotFound", 0)]
    public async Task Resend_Code_That_Isnt_Sent_Answers_The_Same(string outcome, int retryAfterSeconds)
    {
        // Arrange
        (_, var ticket) = await SignUpUnconfirmedAsync();

        StubHttpServer.AnswerRetry(outcome, retryAfterSeconds);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/resend-code", new { Ticket = ticket });

        // Assert
        await AssertCodeResendRequestedAsync(response);
    }

    [Fact]
    public async Task Resend_Code_With_An_Unknown_Ticket_Or_Reset_Answers_Ok_And_Asks_For_No_Real_Code()
    {
        // Arrange
        StubHttpServer.AnswerRetry("NotFound");

        // Act
        var ticketResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/resend-code",
            new { Ticket = RandomStrings.Generate(32) }
        );
        var resetResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/resend-code",
            new { ResetRequestId = RandomStrings.Generate(32) }
        );

        // Assert
        await AssertCodeResendRequestedAsync(ticketResponse);
        await AssertCodeResendRequestedAsync(resetResponse);

        var resends = StubHttpServer.ReceivedBodies(CodeCheckStub.RetryPath);

        Assert.Equal(2, resends.Count);
        Assert.All(resends, resend => Assert.True(Guid.TryParse($"{resend["correlationId"]}", out _)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Resend_Code_Needs_Exactly_One_Of_Ticket_And_Reset_Request_Id(bool withTicket, bool withResetRequestId)
    {
        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/resend-code",
            new { Ticket = withTicket ? "ticket" : null, ResetRequestId = withResetRequestId ? "reset" : null }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorList = jsonObject?["error"]?["errorList"]?.ToObject<List<ValidationError>>();

        Assert.Contains(errorList ?? [], error => error.Message == ErrorMessages.Users.TicketOrResetRequestIdRequired);
        Assert.Empty(StubHttpServer.Requests);
    }

    private async Task<(string UserId, string Ticket)> SignUpUnconfirmedAsync()
    {
        var registerResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/register",
            new
            {
                UserSettingHelper.FirstName,
                UserSettingHelper.UserName,
                UserSettingHelper.UserEmail,
                UserSettingHelper.UserPassword,
            }
        );
        var registered = SerDeHelper.Deserialize<JObject>(await registerResponse.ValidateRequestResponseAsync());
        var ticket = $"{registered?["ticket"]}";

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal("ConfirmEmail", $"{registered?["nextStep"]}");
        Assert.False(string.IsNullOrEmpty(ticket));

        return ($"{registered?["user"]?["id"]}", ticket);
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
    [InlineData("/api/v1/user/confirm")]
    [InlineData("/api/v1/user/resend-code")]
    [InlineData("/api/v1/user/two-factor/verify")]
    [InlineData("/api/v1/user/two-factor/setup")]
    [InlineData("/api/v1/user/two-factor/setup/confirm")]
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
        Assert.Equal(TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id, passwordResetEntity.AppId);
        Assert.False(passwordResetEntity.Applied);

        var message = await NatsTestHelper.FindMessageAsync(
            NatsTestHelper.PasswordResetRequestedSubject,
            passwordResetEntity.Id.ToString()
        );

        Assert.NotNull(message);
        Assert.Equal(UserMessages.PasswordResetAction, message.Action);
        Assert.NotNull(message.ExpiresAt);
        Assert.NotNull(message.GetData<UserMessageData>()?.RequestedAt);
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
        Assert.Equal(1, TestDbHelper.CountPasswordResetEntities(UserSettingHelper.UserId));

        var passwordReset = TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)!;

        Assert.NotNull(await NatsTestHelper.FindMessageAsync(NatsTestHelper.PasswordResetRequestedSubject, passwordReset.Id.ToString()));
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
    public async Task Update_Password_With_The_Right_Code_Updates_Password_Ends_Sessions_And_Tickets_And_Signs_In()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/login",
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );

        user.EmailConfirmed = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        user.AccessFailedCount = 3;
        TestDbHelper.UpdateIdentityUser(user);

        var resetRequestId = await RequestPasswordResetAsync();
        var passwordReset = TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)!;
        var newPassword = "N3wP@ssword!";

        StubHttpServer.AnswerVerify("Verified");

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword = newPassword,
            }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(jsonObject);
        Assert.Equal("Password updated successfully", $"{jsonObject["statusMessage"]}");
        Assert.Equal("Session", $"{jsonObject["nextStep"]}");
        Assert.Equal(UserSettingHelper.UserId, $"{jsonObject["user"]?["id"]}");
        Assert.False(string.IsNullOrEmpty($"{jsonObject["userToken"]?["accessToken"]?["token"]}"));
        Assert.False(string.IsNullOrEmpty($"{jsonObject["userToken"]?["refreshToken"]}"));

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));
        Assert.Equal(UserMessages.PasswordResetAction, $"{check["action"]}");
        Assert.Equal(passwordReset.Id.ToString(), $"{check["correlationId"]}");
        Assert.Equal("123456", $"{check["code"]}");

        Assert.True(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));

        var updatedUser = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

        Assert.Null(updatedUser?.LockoutEnd);
        Assert.Equal(0, updatedUser?.AccessFailedCount);

        var passwordChanged = await FindMessageSentToAsync(NatsTestHelper.PasswordChangedSubject, UserSettingHelper.UserEmail);

        Assert.NotNull(passwordChanged);
        Assert.Equal(UserMessages.PasswordChangedAction, passwordChanged.Action);
        Assert.Equal(UserSettingHelper.UserEmail, passwordChanged.Recipient?.Email);
        Assert.NotNull(passwordChanged.GetData<UserMessageData>()?.ChangedAt);

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
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword = "An0therP@ssword!",
            }
        );

        await AssertCodeErrorAsync(reuseResponse, ErrorMessages.Codes.NotAccepted);
    }

    [Fact]
    public async Task Update_Password_With_A_Wrong_Code_Keeps_The_Password_And_Gives_The_One_Code_Message()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = await RequestPasswordResetAsync();

        StubHttpServer.AnswerVerify("WrongCode", 4);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new
            {
                ResetRequestId = resetRequestId,
                Code = "000000",
                NewPassword = "N3wP@ssword!",
            }
        );

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);
        Assert.False(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);

        await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);
    }

    [Fact]
    public async Task Update_Password_That_Fails_Part_Way_Leaves_Password_Sessions_And_Reset_Request_As_They_Were()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = RandomStrings.Generate(32);
        TestDbHelper.CreatePasswordResetEntity(resetRequestId);

        var originalPasswordHash = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.PasswordHash;

        StubHttpServer.AnswerVerify("Verified");

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
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword = "N3wP@ssword!",
            }
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

        Assert.Equal(3, errorList?.Count);

        Assert.True(errorList?.Any(x => x.Field == "NewPassword" && x.Message == "The NewPassword field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "ResetRequestId" && x.Message == "The ResetRequestId field is required.") ?? false);
        Assert.True(errorList?.Any(x => x.Field == "Code" && x.Message == "The Code field is required.") ?? false);
    }

    [Fact]
    public async Task Update_Password_With_Unknown_Reset_Request_Checks_Only_A_Random_Code_And_Keeps_The_Password()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        StubHttpServer.AnswerVerify("Verified");

        var requestPayload = new
        {
            ResetRequestId = RandomStrings.Generate(32),
            Code = "123456",
            NewPassword = "N3wP@ssword!",
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.True(Guid.TryParse($"{check["correlationId"]}", out _));

        await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);
    }

    [Theory]
    [InlineData(true, 15)]
    [InlineData(false, -1)]
    public async Task Update_Password_With_Used_Or_Expired_Reset_Request_Checks_Only_A_Random_Code_And_Keeps_The_Password(
        bool applied,
        int expiresInMinutes
    )
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        StubHttpServer.AnswerVerify("Verified");

        var resetRequestId = RandomStrings.Generate(32);
        var passwordReset = TestDbHelper.CreatePasswordResetEntity(resetRequestId, applied, DateTime.UtcNow.AddMinutes(expiresInMinutes));

        var requestPayload = new
        {
            ResetRequestId = resetRequestId,
            Code = "123456",
            NewPassword = "N3wP@ssword!",
        };

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/update-password", requestPayload);

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.NotEqual(passwordReset.Id.ToString(), $"{check["correlationId"]}");

        await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);
    }

    [Fact]
    public async Task Update_Password_With_Another_Apps_Reset_Request_Checks_Only_A_Random_Code()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        StubHttpServer.AnswerVerify("Verified");

        var resetRequestId = RandomStrings.Generate(32);
        var passwordReset = TestDbHelper.CreatePasswordResetEntity(resetRequestId);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/update-password",
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword = "N3wP@ssword!",
            },
            TestingWebAppFactory.OtherAppName
        );

        // Assert
        await AssertCodeErrorAsync(response, ErrorMessages.Codes.NotAccepted);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.NotEqual(passwordReset.Id.ToString(), $"{check["correlationId"]}");
        Assert.False(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
    }

    [Fact]
    public async Task Update_Password_With_Password_Not_Meeting_Rules_Returns_Validation_Errors_Before_Checking_The_Code()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = RandomStrings.Generate(32);
        TestDbHelper.CreatePasswordResetEntity(resetRequestId);

        var requestPayload = new
        {
            ResetRequestId = resetRequestId,
            Code = "123456",
            NewPassword = "12345",
        };

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

        Assert.Empty(StubHttpServer.Requests);
        Assert.False(TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Applied);
    }

    [Fact]
    public async Task Resend_Code_With_A_Reset_Request_Id_Asks_For_The_Reset_Code_By_The_Reset_Id()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var resetRequestId = await RequestPasswordResetAsync();
        var passwordReset = TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)!;

        StubHttpServer.AnswerRetry("Sent");

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/resend-code",
            new { ResetRequestId = resetRequestId }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var resend = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.RetryPath));
        Assert.Equal(UserMessages.PasswordResetAction, $"{resend["action"]}");
        Assert.Equal(passwordReset.Id.ToString(), $"{resend["correlationId"]}");
    }

    private async Task<string> RequestPasswordResetAsync()
    {
        var resetResponse = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            _baseUri + "/reset-password",
            new { UserSettingHelper.UserName }
        );

        var resetRequestId = SerDeHelper
            .Deserialize<JObject>(await resetResponse.ValidateRequestResponseAsync())
            ?["resetPasswordDetails"]?["resetRequestId"]?.ToString();

        Assert.NotNull(resetRequestId);

        return resetRequestId;
    }

    private static async Task AssertCodeErrorAsync(HttpResponseMessage response, string message)
    {
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, $"{jsonObject?["error"]?["message"]}");
    }

    private static async Task AssertCodeResendRequestedAsync(HttpResponseMessage response)
    {
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Request Successful", $"{jsonObject?["requestStatus"]}");
        Assert.Equal("Code resend requested", $"{jsonObject?["statusMessage"]}");
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

    private Task<HttpResponseMessage> RegisterAsync(Dictionary<string, string?> fields) =>
        HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, _baseUri + "/register", fields);

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
}
