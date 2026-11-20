using System.Net;
using Newtonsoft.Json.Linq;
using PolyzenKit.Infrastructure.Security.Tokens;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Identity;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class TwoFactorEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";
    private const string NewPassword = "N3wP@ssword!";
    private const string PhoneNumber = "+254700000123";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    [Fact]
    public async Task Required_Two_Step_Sign_In_Proves_The_Email_Then_Sets_Up_An_Authenticator_With_Protected_Recovery_Codes()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactor: true);

        StubHttpServer.AnswerVerify("Verified");

        // Act
        (var loginResponse, var login) = await PostAsync("/login", LoginBody());
        (_, var setUp) = await PostAsync("/confirm", new { Ticket = $"{login["ticket"]}", Code = "123456" });
        (_, var start) = await PostAsync("/two-factor/setup", new { Ticket = $"{setUp["ticket"]}", Method = "Authenticator" });

        var key = $"{start["authenticatorKey"]}";

        (var sessionResponse, var session) = await PostAsync(
            "/two-factor/setup/confirm",
            new { Ticket = $"{setUp["ticket"]}", Code = TotpCodes.For(key) }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal("ConfirmEmail", $"{login["nextStep"]}");
        Assert.Equal("SetUpSecondFactor", $"{setUp["nextStep"]}");
        Assert.Equal(["Authenticator", "Email", "Sms"], setUp["allowedMethods"]?.ToObject<List<string>>());
        Assert.Equal("Authenticator", $"{start["method"]}");
        Assert.StartsWith(
            "otpauth://totp/TestApp:test.user%40domain.com?secret=",
            $"{start["authenticatorUri"]}",
            StringComparison.Ordinal
        );

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.False(string.IsNullOrEmpty($"{session["userToken"]?["accessToken"]?["token"]}"));

        var recoveryCodes = session["recoveryCodes"]?.ToObject<List<string>>() ?? [];

        Assert.Equal(10, recoveryCodes.Count);
        Assert.Equal(TwoFactorMethod.Authenticator, TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));
        Assert.True(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.TwoFactorEnabled);
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));

        var tokens = TestDbHelper.GetUserTokensWithNoTracking(UserSettingHelper.UserId);
        var storedKey = Assert.Single(tokens, token => token.Name == UserTokenNames.AuthenticatorKey);
        var storedCodes = Assert.Single(tokens, token => token.Name == UserTokenNames.RecoveryCodes);

        Assert.StartsWith("enc:", storedKey.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(key, storedKey.Value, StringComparison.Ordinal);
        Assert.StartsWith("enc:", storedCodes.Value, StringComparison.Ordinal);
        Assert.All(recoveryCodes, code => Assert.DoesNotContain(code, storedCodes.Value, StringComparison.Ordinal));

        var changed = await FindMessageSentToAsync(NatsTestHelper.TwoFactorChangedSubject, UserSettingHelper.UserEmail);

        Assert.NotNull(changed);
        Assert.Equal(UserMessages.TwoFactorChangedAction, changed.Action);
        Assert.Equal(nameof(TwoFactorChange.SetUp), changed.GetData<UserMessageData>()?.Change);
        Assert.Equal(TwoFactorMethod.Authenticator, changed.GetData<UserMessageData>()?.Method);
    }

    [Fact]
    public async Task Sign_In_With_An_Authenticator_Asks_For_Its_Code_And_Starts_A_Session_With_The_Right_One()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (var key, _, _) = await SetUpAuthenticatorAsync();

        // Act
        (_, var login) = await PostAsync("/login", LoginBody());
        (var response, var session) = await PostAsync(
            "/two-factor/verify",
            new { Ticket = $"{login["ticket"]}", Code = TotpCodes.For(key) }
        );

        // Assert
        Assert.Equal("SecondFactor", $"{login["nextStep"]}");
        Assert.Equal("Authenticator", $"{login["method"]}");
        Assert.Null(login["codeSentTo"]);
        Assert.Null(login["userToken"]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.False(string.IsNullOrEmpty($"{session["userToken"]?["accessToken"]?["token"]}"));
        Assert.Empty(StubHttpServer.Requests);
    }

    [Fact]
    public async Task A_Wrong_Authenticator_Code_Counts_Toward_Lockout_And_The_Daily_Cap_And_Keeps_The_Ticket()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await SetUpAuthenticatorAsync();

        (_, var login) = await PostAsync("/login", LoginBody());

        // Act
        (var response, var jsonObject) = await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", Code = "000000" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.Codes.NotAccepted, $"{jsonObject["error"]?["message"]}");
        Assert.Equal(1, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount);
        Assert.Equal(1, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.FailedSecondStep));
        Assert.Equal(1, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task A_Recovery_Code_Signs_In_Once()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (_, var recoveryCodes, _) = await SetUpAuthenticatorAsync();

        (_, var firstLogin) = await PostAsync("/login", LoginBody());
        (_, var secondLogin) = await PostAsync("/login", LoginBody());

        // Act
        (var firstResponse, var session) = await PostAsync(
            "/two-factor/verify",
            new { Ticket = $"{firstLogin["ticket"]}", RecoveryCode = recoveryCodes[0] }
        );
        (var secondResponse, _) = await PostAsync(
            "/two-factor/verify",
            new { Ticket = $"{secondLogin["ticket"]}", RecoveryCode = recoveryCodes[0] }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);

        var status = await SendWithTokenAsync(HttpMethod.Get, "/two-factor", $"{session["userToken"]?["accessToken"]?["token"]}");

        Assert.Equal(9, status.Body["recoveryCodesLeft"]?.Value<int>());
    }

    [Theory]
    [InlineData("WrongCode", false)]
    [InlineData("Expired", false)]
    [InlineData("Verified", true)]
    public async Task Sign_In_With_The_Email_Method_Checks_The_Emailed_Login_Code(string outcome, bool signsIn)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        StubHttpServer.AnswerVerify(outcome, 4);

        (_, var login) = await PostAsync("/login", LoginBody());
        var loginTicket = Assert.Single(TestDbHelper.GetLoginTicketEntitiesWithNoTracking(UserSettingHelper.UserId));

        // Act
        (var response, var jsonObject) = await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", Code = "123456" });

        // Assert
        Assert.Equal("SecondFactor", $"{login["nextStep"]}");
        Assert.Equal("Email", $"{login["method"]}");
        Assert.Equal("t***@domain.com", $"{login["codeSentTo"]}");

        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, loginTicket.Id.ToString());

        Assert.NotNull(loginCode);
        Assert.Equal(LoginCodePurpose.SignIn, loginCode.GetData<UserMessageData>()?.Purpose);
        Assert.Equal(UserSettingHelper.UserEmail, loginCode.Recipient?.Email);
        Assert.Null(loginCode.Recipient?.Phone);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.Equal(UserMessages.LoginCodeAction, $"{check["action"]}");
        Assert.Equal(loginTicket.Id.ToString(), $"{check["correlationId"]}");

        if (signsIn)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Session", $"{jsonObject["nextStep"]}");
            return;
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.Codes.NotAccepted, $"{jsonObject["error"]?["message"]}");
        Assert.Equal(
            outcome == "WrongCode" ? 1 : 0,
            TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.FailedSecondStep)
        );
        Assert.Equal(
            outcome == "WrongCode" ? 1 : 0,
            TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount
        );
    }

    [Fact]
    public async Task Sign_In_With_The_Text_Method_Texts_The_Login_Code_To_The_Phone_Only()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Sms);

        // Act
        (_, var login) = await PostAsync("/login", LoginBody());

        // Assert
        var loginTicket = Assert.Single(TestDbHelper.GetLoginTicketEntitiesWithNoTracking(UserSettingHelper.UserId));
        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, loginTicket.Id.ToString());

        Assert.Equal("SecondFactor", $"{login["nextStep"]}");
        Assert.Equal("Sms", $"{login["method"]}");
        Assert.Equal(ContactMask.PhoneNumber(UserSettingHelper.PhoneNumber), $"{login["codeSentTo"]}");
        Assert.NotNull(loginCode);
        Assert.Null(loginCode.Recipient?.Email);
        Assert.Equal(UserSettingHelper.PhoneNumber, loginCode.Recipient?.Phone);
    }

    [Fact]
    public async Task A_Method_The_App_No_Longer_Allows_Counts_As_No_Method()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(sms: false);
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Sms);

        // Act
        (_, var login) = await PostAsync("/login", LoginBody());

        // Assert
        Assert.Equal("Session", $"{login["nextStep"]}");
    }

    [Fact]
    public async Task Five_Wrong_Second_Step_Codes_Lock_The_Account()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.LockoutEnabled = true;
        TestDbHelper.UpdateIdentityUser(user);

        StubHttpServer.AnswerVerify("WrongCode", 4);

        (_, var login) = await PostAsync("/login", LoginBody());

        for (var attempt = 0; attempt < 5; attempt++)
            await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", Code = "000000" });

        // Act
        (var response, _) = await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", Code = "000000" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(AccountLimitKind.FailedSecondStep)]
    [InlineData(AccountLimitKind.LoginCodeSent)]
    public async Task Sign_In_Past_A_Daily_Cap_Answers_The_Lockout_And_Sends_No_Code(AccountLimitKind kind)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);
        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, kind, 10);

        // Act
        (var response, var jsonObject) = await PostAsync("/login", LoginBody());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Provided credentials are invalid", $"{jsonObject["error"]?["message"]}");
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(UserSettingHelper.UserId));
        Assert.Equal(
            kind == AccountLimitKind.LoginCodeSent ? 10 : 0,
            TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent)
        );
    }

    [Fact]
    public async Task Verify_Past_The_Daily_Failed_Second_Step_Cap_Answers_The_Lockout_Without_Checking_The_Code()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        StubHttpServer.AnswerVerify("Verified");

        (_, var login) = await PostAsync("/login", LoginBody());

        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.FailedSecondStep, 10);

        // Act
        (var response, _) = await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", Code = "123456" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(StubHttpServer.Requests);
    }

    [Fact]
    public async Task Text_Message_Setup_Texts_A_Code_To_The_New_Phone_And_Stores_It_Confirmed()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactor: true);

        StubHttpServer.AnswerVerify("Verified");

        var setUpTicket = await ReachSetUpAsync();

        // Act
        (_, var start) = await PostAsync(
            "/two-factor/setup",
            new
            {
                Ticket = setUpTicket,
                Method = "Sms",
                PhoneNumber,
            }
        );
        var loginTicket = Assert.Single(TestDbHelper.GetLoginTicketEntitiesWithNoTracking(UserSettingHelper.UserId));
        (var response, var session) = await PostAsync("/two-factor/setup/confirm", new { Ticket = setUpTicket, Code = "654321" });

        // Assert
        Assert.Equal("Sms", $"{start["method"]}");
        Assert.Equal("***123", $"{start["codeSentTo"]}");
        Assert.Equal(PhoneNumber, loginTicket.PhoneNumber);
        Assert.Equal(TwoFactorMethod.Sms, loginTicket.Method);

        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, loginTicket.Id.ToString());

        Assert.NotNull(loginCode);
        Assert.Equal(LoginCodePurpose.SetUp, loginCode.GetData<UserMessageData>()?.Purpose);
        Assert.Equal(PhoneNumber, loginCode.Recipient?.Phone);
        Assert.Null(loginCode.Recipient?.Email);

        var check = StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath)[^1];

        Assert.Equal(loginTicket.Id.ToString(), $"{check["correlationId"]}");
        Assert.Equal("654321", $"{check["code"]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.Equal(10, session["recoveryCodes"]?.ToObject<List<string>>()?.Count);

        var user = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

        Assert.Equal(PhoneNumber, user?.PhoneNumber);
        Assert.True(user?.PhoneNumberConfirmed);
        Assert.Equal(TwoFactorMethod.Sms, TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Email_Setup_Needs_No_Code_And_Still_Makes_Recovery_Codes()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactor: true);

        StubHttpServer.AnswerVerify("Verified");

        var setUpTicket = await ReachSetUpAsync();

        // Act
        (_, var start) = await PostAsync("/two-factor/setup", new { Ticket = setUpTicket, Method = "Email" });
        (var response, var session) = await PostAsync("/two-factor/setup/confirm", new { Ticket = setUpTicket });

        // Assert
        Assert.Equal("Email", $"{start["method"]}");
        Assert.Null(start["codeSentTo"]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.Equal(10, session["recoveryCodes"]?.ToObject<List<string>>()?.Count);
        Assert.Equal(TwoFactorMethod.Email, TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));
        Assert.DoesNotContain(
            TestDbHelper.GetUserTokensWithNoTracking(UserSettingHelper.UserId),
            token => token.Name == UserTokenNames.AuthenticatorKey
        );
    }

    [Fact]
    public async Task Setup_Refuses_A_Method_The_App_Doesnt_Allow()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactor: true, sms: false);

        StubHttpServer.AnswerVerify("Verified");

        var setUpTicket = await ReachSetUpAsync();

        // Act
        (var response, var jsonObject) = await PostAsync(
            "/two-factor/setup",
            new
            {
                Ticket = setUpTicket,
                Method = "Sms",
                PhoneNumber,
            }
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.MethodNotAllowed, $"{jsonObject["error"]?["message"]}");
    }

    [Fact]
    public async Task Setup_Confirm_Before_Choosing_A_Method_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactor: true);

        StubHttpServer.AnswerVerify("Verified");

        var setUpTicket = await ReachSetUpAsync();

        // Act
        (var response, var jsonObject) = await PostAsync("/two-factor/setup/confirm", new { Ticket = setUpTicket, Code = "123456" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.ChooseMethodFirst, $"{jsonObject["error"]?["message"]}");
    }

    [Fact]
    public async Task A_Sign_In_Ticket_Works_Only_On_The_Step_It_Was_Made_For()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        (_, var login) = await PostAsync("/login", LoginBody());

        // Act
        (var response, var jsonObject) = await PostAsync("/two-factor/setup", new { Ticket = $"{login["ticket"]}", Method = "Email" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.Codes.NotAccepted, $"{jsonObject["error"]?["message"]}");
    }

    [Theory]
    [InlineData(true, "ConfirmEmail")]
    [InlineData(false, "Session")]
    public async Task Two_Step_Sign_In_Required_For_Admins_Applies_Only_To_Admin_Accounts(bool isAdmin, string nextStep)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync(requireTwoFactorForAdmins: true);

        if (isAdmin)
            TestDbHelper.ConfigureIdentityUserAsAdmin();

        // Act
        (_, var login) = await PostAsync("/login", LoginBody());

        // Assert
        Assert.Equal(nextStep, $"{login["nextStep"]}");
    }

    [Fact]
    public async Task Profile_Status_Shows_The_Method_The_Codes_Left_The_Requirement_And_The_Allowed_Methods()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (_, _, var session) = await SetUpAuthenticatorAsync();

        // Act
        var status = await SendWithTokenAsync(HttpMethod.Get, "/two-factor", AccessTokenOf(session));

        // Assert
        Assert.Equal(HttpStatusCode.OK, status.Response.StatusCode);
        Assert.Equal("Authenticator", $"{status.Body["method"]}");
        Assert.Equal(10, status.Body["recoveryCodesLeft"]?.Value<int>());
        Assert.Equal("True", $"{status.Body["required"]}");
        Assert.Equal(["Authenticator", "Email", "Sms"], status.Body["allowedMethods"]?.ToObject<List<string>>());
    }

    [Fact]
    public async Task Changing_The_Method_Proves_The_Email_Then_Sets_Up_The_New_Method()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();

        (_, var firstSession) = await PostAsync("/login", LoginBody());
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        StubHttpServer.AnswerVerify("Verified");

        // Act
        var change = await SendWithTokenAsync(
            HttpMethod.Post,
            "/two-factor/change",
            AccessTokenOf(firstSession),
            new { Password = UserSettingHelper.UserPassword }
        );
        (_, var setUp) = await PostAsync("/confirm", new { Ticket = $"{change.Body["ticket"]}", Code = "123456" });
        (_, var start) = await PostAsync("/two-factor/setup", new { Ticket = $"{setUp["ticket"]}", Method = "Authenticator" });
        (_, var session) = await PostAsync(
            "/two-factor/setup/confirm",
            new { Ticket = $"{setUp["ticket"]}", Code = TotpCodes.For($"{start["authenticatorKey"]}") }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, change.Response.StatusCode);
        Assert.Equal("ConfirmEmail", $"{change.Body["nextStep"]}");
        Assert.Equal("t***@domain.com", $"{change.Body["codeSentTo"]}");
        Assert.Equal("SetUpSecondFactor", $"{setUp["nextStep"]}");
        Assert.Equal("Session", $"{session["nextStep"]}");
        Assert.Equal(TwoFactorMethod.Authenticator, TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));

        var changed = await FindMessageSentToAsync(NatsTestHelper.TwoFactorChangedSubject, UserSettingHelper.UserEmail);

        Assert.Equal(nameof(TwoFactorChange.Changed), changed?.GetData<UserMessageData>()?.Change);
    }

    [Fact]
    public async Task Changing_The_Method_With_A_Wrong_Password_Is_Refused_And_Counts_Toward_Lockout()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();

        (_, var session) = await PostAsync("/login", LoginBody());

        // Act
        var change = await SendWithTokenAsync(
            HttpMethod.Post,
            "/two-factor/change",
            AccessTokenOf(session),
            new { Password = "Wr0ng-Password" }
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, change.Response.StatusCode);
        Assert.Equal(ErrorMessages.Users.WrongPassword, $"{change.Body["error"]?["message"]}");
        Assert.Equal(1, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount);
    }

    [Fact]
    public async Task New_Recovery_Codes_Replace_The_Old_Ones()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (_, var oldCodes, var session) = await SetUpAuthenticatorAsync();

        // Act
        var replaced = await SendWithTokenAsync(
            HttpMethod.Post,
            "/two-factor/recovery-codes",
            AccessTokenOf(session),
            new { Password = UserSettingHelper.UserPassword }
        );

        (_, var login) = await PostAsync("/login", LoginBody());
        (var oldCodeResponse, _) = await PostAsync("/two-factor/verify", new { Ticket = $"{login["ticket"]}", RecoveryCode = oldCodes[0] });

        // Assert
        var newCodes = replaced.Body["recoveryCodes"]?.ToObject<List<string>>() ?? [];

        Assert.Equal(HttpStatusCode.OK, replaced.Response.StatusCode);
        Assert.Equal(10, newCodes.Count);
        Assert.Empty(newCodes.Intersect(oldCodes));
        Assert.Equal(HttpStatusCode.BadRequest, oldCodeResponse.StatusCode);
    }

    [Fact]
    public async Task Turning_Off_Two_Step_Sign_In_Is_Refused_While_It_Is_Required()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (_, _, var session) = await SetUpAuthenticatorAsync();

        // Act
        var response = await SendWithTokenAsync(
            HttpMethod.Post,
            "/two-factor/disable",
            AccessTokenOf(session),
            new { Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.Required, $"{response.Body["error"]?["message"]}");
        Assert.Equal(TwoFactorMethod.Authenticator, TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Turning_Off_Two_Step_Sign_In_Removes_The_Method_Key_And_Codes()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        (_, _, var session) = await SetUpAuthenticatorAsync();
        await AllowTwoFactorAsync();

        // Act
        var response = await SendWithTokenAsync(
            HttpMethod.Post,
            "/two-factor/disable",
            AccessTokenOf(session),
            new { Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Null(response.Body["method"]);
        Assert.Null(TestDbHelper.GetTwoFactorMethod(UserSettingHelper.UserId));
        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.TwoFactorEnabled);
        Assert.Empty(TestDbHelper.GetUserTokensWithNoTracking(UserSettingHelper.UserId));

        var turnedOff = await NatsTestHelper.FindMessageAsync(
            NatsTestHelper.TwoFactorChangedSubject,
            message =>
                message.Recipient?.Email == UserSettingHelper.UserEmail
                && message.OccurredAt >= TestStartedAt.AddSeconds(-1)
                && message.GetData<UserMessageData>()?.Change == nameof(TwoFactorChange.TurnedOff)
        );

        Assert.NotNull(turnedOff);
    }

    [Fact]
    public async Task Refresh_Is_Refused_When_Two_Step_Sign_In_Became_Required_And_The_User_Has_No_Method()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var userToken, var refreshToken) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        await AllowTwoFactorAsync(requireTwoFactor: true);

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            BaseUri + "/refresh-token",
            new { AccessToken = userToken, RefreshToken = refreshToken }
        );

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(
            TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId),
            token => token.Token == new TokenFactory().HashToken(refreshToken)
        );
    }

    [Fact]
    public async Task Password_Reset_Of_An_Authenticator_User_Applies_The_Password_Then_Asks_For_The_Second_Step()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await SetUpAuthenticatorAsync();

        var resetRequestId = await RequestPasswordResetAsync();

        StubHttpServer.AnswerVerify("Verified");

        // Act
        (var response, var jsonObject) = await PostAsync(
            "/update-password",
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword,
            }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SecondFactor", $"{jsonObject["nextStep"]}");
        Assert.Equal("Authenticator", $"{jsonObject["method"]}");
        Assert.Null(jsonObject["userToken"]);

        (_, var login) = await PostAsync("/login", new { UserSettingHelper.UserName, Password = NewPassword });

        Assert.Equal("SecondFactor", $"{login["nextStep"]}");
    }

    [Fact]
    public async Task Password_Reset_Of_An_Email_Method_User_Starts_A_Session()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        await AllowTwoFactorAsync();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);

        var resetRequestId = await RequestPasswordResetAsync();

        StubHttpServer.AnswerVerify("Verified");

        // Act
        (var response, var jsonObject) = await PostAsync(
            "/update-password",
            new
            {
                ResetRequestId = resetRequestId,
                Code = "123456",
                NewPassword,
            }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Session", $"{jsonObject["nextStep"]}");
    }

    [Fact]
    public async Task App_Admin_Resets_A_Users_Two_Step_Sign_In_And_Ends_Their_Sessions()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        await AllowTwoFactorAsync();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        TestDbHelper.SetTwoFactorMethod(targetUserId, TwoFactorMethod.Email);
        TestDbHelper.CreateRefreshTokenEntity(targetUserId);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            BaseUri + "/two-factor/reset",
            new { Email = "other.app.user@example.com" }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(TestDbHelper.GetTwoFactorMethod(targetUserId));
        Assert.Empty(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(targetUserId));

        var reset = await FindMessageSentToAsync(NatsTestHelper.TwoFactorChangedSubject, "other.app.user@example.com");
        var data = reset?.GetData<UserMessageData>();

        Assert.Equal(nameof(TwoFactorChange.Reset), data?.Change);
        Assert.Equal(TwoFactorMethod.Email, data?.Method);
        Assert.Null(data?.IpAddress);
        Assert.Null(data?.Device);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task Only_A_Global_Admin_Resets_An_Admins_Two_Step_Sign_In(bool callerIsGlobalAdmin, HttpStatusCode statusCode)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        if (callerIsGlobalAdmin)
            TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.AdminRoles.First(), targetUserId);
        TestDbHelper.SetTwoFactorMethod(targetUserId, TwoFactorMethod.Email);

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            BaseUri + "/two-factor/reset",
            new { Email = "other.app.user@example.com" }
        );

        // Assert
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(callerIsGlobalAdmin ? null : TwoFactorMethod.Email, TestDbHelper.GetTwoFactorMethod(targetUserId));
    }

    [Fact]
    public async Task Resetting_An_Unknown_Email_Or_Another_Apps_User_Returns_Not_Found()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        // Act
        var unknown = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            BaseUri + "/two-factor/reset",
            new { Email = "nobody@example.com" }
        );
        var otherApp = await HttpClient.SendValidAuthRequestAsync(
            HttpMethod.Post,
            BaseUri + "/two-factor/reset",
            new { Email = "other.app.user@example.com" }
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherApp.StatusCode);
    }

    private async Task AllowTwoFactorAsync(
        bool requireTwoFactor = false,
        bool requireTwoFactorForAdmins = false,
        bool authenticator = true,
        bool email = true,
        bool sms = true
    ) =>
        await _testingWebAppFactory.UpdateSignInSettingsAsync(
            TestingWebAppFactory.TestAppName,
            new UpdateSignInSettingsCommand
            {
                RequireConfirmedAccount = true,
                DefaultRole = TestingWebAppFactory.DefaultRole,
                RequireTwoFactor = requireTwoFactor,
                RequireTwoFactorForAdmins = requireTwoFactorForAdmins,
                AllowAuthenticatorTwoFactor = authenticator,
                AllowEmailTwoFactor = email,
                AllowSmsTwoFactor = sms,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<string> ReachSetUpAsync()
    {
        (_, var login) = await PostAsync("/login", LoginBody());
        (_, var setUp) = await PostAsync("/confirm", new { Ticket = $"{login["ticket"]}", Code = "123456" });

        Assert.Equal("SetUpSecondFactor", $"{setUp["nextStep"]}");

        return $"{setUp["ticket"]}";
    }

    private async Task<(string Key, List<string> RecoveryCodes, JObject Session)> SetUpAuthenticatorAsync()
    {
        await AllowTwoFactorAsync(requireTwoFactor: true);

        StubHttpServer.AnswerVerify("Verified");

        var setUpTicket = await ReachSetUpAsync();

        (_, var start) = await PostAsync("/two-factor/setup", new { Ticket = setUpTicket, Method = "Authenticator" });

        var key = $"{start["authenticatorKey"]}";

        (_, var session) = await PostAsync("/two-factor/setup/confirm", new { Ticket = setUpTicket, Code = TotpCodes.For(key) });

        Assert.Equal("Session", $"{session["nextStep"]}");

        StubHttpServer.Reset();

        return (key, session["recoveryCodes"]?.ToObject<List<string>>() ?? [], session);
    }

    private async Task<string> RequestPasswordResetAsync()
    {
        (_, var reset) = await PostAsync("/reset-password", new { UserSettingHelper.UserName });

        return $"{reset["resetPasswordDetails"]?["resetRequestId"]}";
    }

    private static object LoginBody() => new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword };

    private static string AccessTokenOf(JObject session) => $"{session["userToken"]?["accessToken"]?["token"]}";

    private async Task<(HttpResponseMessage Response, JObject Body)> PostAsync(string path, object body)
    {
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Post, BaseUri + path, body);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> SendWithTokenAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null
    )
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(method, BaseUri + path);
        httpRequest.AddAuthHeader(accessToken);

        if (body is not null)
            httpRequest.Content = SerDeHelper.ConvertToHttpContent(body);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
