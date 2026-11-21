using System.Net;
using Newtonsoft.Json.Linq;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserDetailsEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";
    private const string NewEmail = "new.address@example.com";
    private const string NewPhoneNumber = "0700000999";
    private const string OtherUserEmail = "other.app.user@example.com";
    private const string Device = "TestBrowser/1.0";

    [Fact]
    public async Task A_User_Updates_Their_Names_And_Phone_And_The_New_Phone_Is_Unconfirmed()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.PhoneNumberConfirmed = true;
        TestDbHelper.UpdateIdentityUser(user);

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Put,
            $"/{UserSettingHelper.UserId}",
            new
            {
                FirstName = "Renamed",
                LastName = "Person",
                PhoneNumber = NewPhoneNumber,
            }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal("Renamed", $"{response.Body["user"]?["firstName"]}");
        Assert.Equal(NewPhoneNumber, $"{response.Body["user"]?["phoneNumber"]}");
        Assert.Equal("Renamed", TestDbHelper.GetUserEntityWithNoTracking(UserSettingHelper.UserId)?.FirstName);

        var updatedUser = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

        Assert.Equal(NewPhoneNumber, updatedUser?.PhoneNumber);
        Assert.False(updatedUser?.PhoneNumberConfirmed);

        var notice = await FindMessageSentToAsync(NatsTestHelper.ContactChangedSubject, UserSettingHelper.UserEmail);
        var data = notice?.GetData<UserMessageData>();

        Assert.Equal(UserMessages.ContactChangedAction, notice?.Action);
        Assert.Equal(nameof(ContactChange.Phone), data?.Change);
        Assert.Equal(Device, data?.Device);
        Assert.NotNull(data?.ChangedAt);
    }

    [Fact]
    public async Task Updating_Only_The_Names_Sends_No_Notice()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Put,
            $"/{UserSettingHelper.UserId}",
            new { FirstName = "Renamed", UserSettingHelper.PhoneNumber }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Null(TestDbHelper.GetUserEntityWithNoTracking(UserSettingHelper.UserId)?.LastName);
        Assert.Null(
            await FindMessageSentToAsync(NatsTestHelper.ContactChangedSubject, UserSettingHelper.UserEmail, TimeSpan.FromSeconds(1))
        );
    }

    [Fact]
    public async Task Changing_The_Phone_Of_The_Text_Method_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Sms);

        var accessToken = await LoginWithoutSecondStepAsync();

        // Act
        var response = await SendWithTokenAsync(
            HttpMethod.Put,
            $"/{UserSettingHelper.UserId}",
            accessToken,
            new { UserSettingHelper.FirstName, PhoneNumber = NewPhoneNumber }
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Users.PhoneUsedForTwoFactor, $"{response.Body["error"]?["message"]}");
        Assert.Equal(UserSettingHelper.PhoneNumber, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.PhoneNumber);
    }

    [Fact]
    public async Task An_App_Admins_Change_Sends_A_Notice_That_Doesnt_Say_Who_Made_It()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Put,
            $"/{targetUserId}",
            new { FirstName = "Helped", PhoneNumber = NewPhoneNumber }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal("Helped", TestDbHelper.GetUserEntityWithNoTracking(targetUserId)?.FirstName);

        var data = (await FindMessageSentToAsync(NatsTestHelper.ContactChangedSubject, OtherUserEmail))?.GetData<UserMessageData>();

        Assert.Equal(nameof(ContactChange.Phone), data?.Change);
        Assert.Null(data?.Device);
        Assert.Null(data?.IpAddress);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task Only_A_Global_Admin_Changes_An_Admins_Details(bool callerIsGlobalAdmin, HttpStatusCode statusCode)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        if (callerIsGlobalAdmin)
            TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.AdminRoles.First(), targetUserId);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Put, $"/{targetUserId}", new { FirstName = "Changed" });

        // Assert
        Assert.Equal(statusCode, response.Response.StatusCode);
        Assert.Equal(callerIsGlobalAdmin ? "Changed" : "Other", TestDbHelper.GetUserEntityWithNoTracking(targetUserId)?.FirstName);
    }

    [Fact]
    public async Task An_App_Admin_Cant_Change_Another_Apps_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var otherAppUserId = TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Put, $"/{otherAppUserId}", new { FirstName = "Changed" });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
    }

    [Fact]
    public async Task An_App_Admin_Finds_An_Account_Of_The_App_By_Its_Email()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await GetAsUserAsync($"/email/{Uri.EscapeDataString(OtherUserEmail)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(targetUserId, $"{response.Body["user"]?["id"]}");
        Assert.Equal(OtherUserEmail, $"{response.Body["user"]?["email"]}");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("other-app")]
    [InlineData("admin")]
    public async Task An_App_Admin_Gets_The_Same_404_For_An_Unknown_Email_Another_Apps_Account_Or_An_Admin(string target)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        if (target == "other-app")
            TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        if (target == "admin")
            TestDbHelper.CreateIdentityUserRole(TestDbHelper.AdminRoles.First(), TestDbHelper.CreateUserOfApp(RegisteredApp.AppName));

        // Act
        var response = await GetAsUserAsync($"/email/{Uri.EscapeDataString(OtherUserEmail)}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
    }

    [Fact]
    public async Task A_Global_Admin_Finds_An_Admin_Account_By_Its_Email()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.AdminRoles.First(), targetUserId);

        // Act
        var response = await GetAsUserAsync($"/email/{Uri.EscapeDataString(OtherUserEmail)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(targetUserId, $"{response.Body["user"]?["id"]}");
    }

    [Fact]
    public async Task A_User_Who_Is_Not_An_Admin_Cant_Find_Accounts_By_Email()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await GetAsUserAsync($"/email/{Uri.EscapeDataString(OtherUserEmail)}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.Response.StatusCode);
    }

    [Fact]
    public async Task A_User_Cant_Change_Another_User()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var otherUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Put, $"/{otherUserId}", new { FirstName = "Changed" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.Response.StatusCode);
    }

    [Fact]
    public async Task A_Users_Own_Email_Change_Waits_For_The_Code_Sent_To_The_New_Address()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        MakeUserNameTheEmail();

        StubHttpServer.AnswerVerify("Verified");

        // Act
        var started = await SendAsUserAsync(
            HttpMethod.Post,
            $"/{UserSettingHelper.UserId}/email",
            new { Email = NewEmail, Password = UserSettingHelper.UserPassword }
        );
        var pending = TestDbHelper.GetEmailChangeEntityWithNoTracking(UserSettingHelper.UserId);
        var emailBeforeConfirm = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.Email;
        var confirmed = await SendAsUserAsync(HttpMethod.Post, $"/{UserSettingHelper.UserId}/email/confirm", new { Code = "123456" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, started.Response.StatusCode);
        Assert.Equal(ContactMask.Email(NewEmail), $"{started.Body["codeSentTo"]}");
        Assert.NotNull(pending);
        Assert.Equal(NewEmail, pending.NewEmail);
        Assert.Equal(UserSettingHelper.UserEmail, emailBeforeConfirm);

        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, pending.Id.ToString());

        Assert.NotNull(loginCode);
        Assert.Equal(LoginCodePurpose.ChangeEmail, loginCode.GetData<UserMessageData>()?.Purpose);
        Assert.Equal(NewEmail, loginCode.Recipient?.Email);
        Assert.Null(loginCode.Recipient?.Phone);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.Equal(UserMessages.LoginCodeAction, $"{check["action"]}");
        Assert.Equal(pending.Id.ToString(), $"{check["correlationId"]}");

        Assert.Equal(HttpStatusCode.OK, confirmed.Response.StatusCode);
        Assert.Equal(NewEmail, $"{confirmed.Body["user"]?["email"]}");

        var updatedUser = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId);

        Assert.Equal(NewEmail, updatedUser?.Email);
        Assert.True(updatedUser?.EmailConfirmed);
        Assert.Equal(NewEmail, updatedUser?.UserName);
        Assert.Null(TestDbHelper.GetEmailChangeEntityWithNoTracking(UserSettingHelper.UserId));

        var notice = await FindMessageSentToAsync(NatsTestHelper.ContactChangedSubject, UserSettingHelper.UserEmail);

        Assert.Equal(nameof(ContactChange.Email), notice?.GetData<UserMessageData>()?.Change);
    }

    [Fact]
    public async Task A_Users_Own_Email_Change_To_A_Taken_Email_Answers_The_Same_And_Sends_No_Code()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Post,
            $"/{UserSettingHelper.UserId}/email",
            new { Email = OtherUserEmail, Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(ContactMask.Email(OtherUserEmail), $"{response.Body["codeSentTo"]}");
        Assert.Null(await FindMessageSentToAsync(NatsTestHelper.LoginCodeRequestedSubject, OtherUserEmail, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent));
    }

    [Theory]
    [InlineData(null, ErrorMessages.Users.PasswordRequired)]
    [InlineData("Wr0ng-Password", ErrorMessages.Users.WrongPassword)]
    public async Task A_Users_Own_Email_Change_Needs_The_Right_Password(string? password, string message)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Post,
            $"/{UserSettingHelper.UserId}/email",
            new { Email = NewEmail, Password = password }
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(message, $"{response.Body["error"]?["message"]}");
        Assert.Null(TestDbHelper.GetEmailChangeEntityWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task A_Users_Own_Email_Change_Past_The_Daily_Login_Code_Cap_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.CreateAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.LoginCodeSent, 10);

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Post,
            $"/{UserSettingHelper.UserId}/email",
            new { Email = NewEmail, Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Users.TooManyCodes, $"{response.Body["error"]?["message"]}");
        Assert.Null(TestDbHelper.GetEmailChangeEntityWithNoTracking(UserSettingHelper.UserId));
    }

    [Theory]
    [InlineData(true, "WrongCode")]
    [InlineData(false, "Verified")]
    public async Task Confirming_An_Email_Change_Without_A_Pending_Change_Or_The_Right_Code_Keeps_The_Email(bool pending, string outcome)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        StubHttpServer.AnswerVerify(outcome, 4);

        if (pending)
            await SendAsUserAsync(
                HttpMethod.Post,
                $"/{UserSettingHelper.UserId}/email",
                new { Email = NewEmail, Password = UserSettingHelper.UserPassword }
            );

        // Act
        var response = await SendAsUserAsync(HttpMethod.Post, $"/{UserSettingHelper.UserId}/email/confirm", new { Code = "000000" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Codes.NotAccepted, $"{response.Body["error"]?["message"]}");
        Assert.Equal(UserSettingHelper.UserEmail, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.Email);
    }

    [Fact]
    public async Task An_Admins_Email_Change_Applies_At_Once_Unconfirmed_And_Tells_The_Old_Address()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Post, $"/{targetUserId}/email", new { Email = NewEmail });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(NewEmail, $"{response.Body["user"]?["email"]}");
        Assert.Null(response.Body["codeSentTo"]);

        var updatedUser = TestDbHelper.GetIdentityUserWithNoTracking(targetUserId);

        Assert.Equal(NewEmail, updatedUser?.Email);
        Assert.False(updatedUser?.EmailConfirmed);
        Assert.Equal("other.app.user", updatedUser?.UserName);

        var notice = await FindMessageSentToAsync(NatsTestHelper.ContactChangedSubject, OtherUserEmail);

        Assert.Equal(nameof(ContactChange.Email), notice?.GetData<UserMessageData>()?.Change);
        Assert.Null(notice?.GetData<UserMessageData>()?.Device);
    }

    [Fact]
    public async Task An_Admins_Email_Change_To_A_Taken_Email_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Post, $"/{targetUserId}/email", new { Email = UserSettingHelper.UserEmail });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Users.EmailNotChangedByAdmin, $"{response.Body["error"]?["message"]}");
        Assert.Equal(OtherUserEmail, TestDbHelper.GetIdentityUserWithNoTracking(targetUserId)?.Email);
    }

    private void MakeUserNameTheEmail()
    {
        var user = TestDbHelper.GetIdentityUser(UserSettingHelper.UserId)!;
        user.UserName = UserSettingHelper.UserEmail;
        user.NormalizedUserName = UserSettingHelper.UserEmail.ToUpperInvariant();
        TestDbHelper.UpdateIdentityUser(user);
    }

    private async Task<string> LoginWithoutSecondStepAsync()
    {
        (var accessToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        return accessToken;
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> SendAsUserAsync(HttpMethod method, string path, object body)
    {
        var loginName = TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.UserName ?? UserSettingHelper.UserName;

        (var accessToken, _) = await HttpClient.LoginUserAsync(loginName, UserSettingHelper.UserPassword);

        return await SendWithTokenAsync(method, path, accessToken, body);
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> GetAsUserAsync(string path)
    {
        (var accessToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Get, BaseUri + path);
        httpRequest.AddAuthHeader(accessToken);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> SendWithTokenAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object body
    )
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(method, BaseUri + path);
        httpRequest.AddAuthHeader(accessToken);
        httpRequest.Headers.UserAgent.ParseAdd(Device);
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(body);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
