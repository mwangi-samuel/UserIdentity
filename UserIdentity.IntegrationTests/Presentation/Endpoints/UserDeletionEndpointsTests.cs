using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserDeletionEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";
    private const string Device = "TestBrowser/1.0";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    [Theory]
    [InlineData(null, ErrorMessages.Users.PasswordRequiredToDeleteAccount)]
    [InlineData("Wr0ng-Password", ErrorMessages.Users.WrongPassword)]
    public async Task Deleting_Your_Own_Account_Needs_Your_Password(string? password, string message)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await SendAsUserAsync(HttpMethod.Delete, $"/{UserSettingHelper.UserId}", new { Password = password });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(message, $"{response.Body["error"]?["message"]}");
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    }

    [Fact]
    public async Task Deleting_Your_Own_Account_Removes_It_With_Its_Sessions_Invite_And_Wait_List_Entry_And_Announces_It()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var invite = TestDbHelper.CreateInviteCodeEntity(UserSettingHelper.UserEmail, RegisteredApp.AppName, applied: true);
        var waitListEntry = TestDbHelper.CreateWaitListEntity(UserSettingHelper.UserEmail, RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Delete,
            $"/{UserSettingHelper.UserId}",
            new { Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
        Assert.Null(TestDbHelper.GetUserEntityWithNoTracking(UserSettingHelper.UserId));
        Assert.Empty(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId));
        Assert.Null(TestDbHelper.GetInviteCodeEntityWithNoTracking(invite.Id));
        Assert.Null(TestDbHelper.GetWaitListEntity(waitListEntry.Id));

        var notice = await FindDeletionNoticeAsync(UserSettingHelper.UserId);
        var data = notice?.GetData<UserMessageData>();

        Assert.Equal(UserMessages.AccountDeletedAction, notice?.Action);
        Assert.Equal(UserSettingHelper.UserEmail, notice?.Recipient?.Email);
        Assert.Equal(Device, data?.Device);
        Assert.NotNull(data?.DeletedAt);
    }

    [Fact]
    public async Task An_App_Admin_Deletes_Accounts_Of_The_App_With_One_Emailed_Code_And_The_Notice_Doesnt_Say_Who()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        await AllowEmailTwoFactorAsync();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        var accessToken = await LoginAsync();

        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);
        StubHttpServer.AnswerVerify("Verified");

        // Act
        var code = await SendAsync(accessToken, HttpMethod.Post, "/step-up/code");
        var stepUp = await SendAsync(accessToken, HttpMethod.Post, "/step-up", new { Code = "123456" });
        var deletion = await SendAsync(accessToken, HttpMethod.Delete, $"/{targetUserId}", new { Ticket = $"{stepUp.Body["ticket"]}" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, code.Response.StatusCode);
        Assert.Equal("Email", $"{code.Body["method"]}");
        Assert.Equal(ContactMask.Email(UserSettingHelper.UserEmail), $"{code.Body["codeSentTo"]}");

        var stepUpEntity = Assert.Single(TestDbHelper.GetStepUpEntitiesWithNoTracking(UserSettingHelper.UserId));
        var loginCode = await NatsTestHelper.FindMessageAsync(NatsTestHelper.LoginCodeRequestedSubject, stepUpEntity.Id.ToString());

        Assert.Equal(LoginCodePurpose.ConfirmAction, loginCode?.GetData<UserMessageData>()?.Purpose);
        Assert.Equal(UserSettingHelper.UserEmail, loginCode?.Recipient?.Email);
        Assert.Null(loginCode?.Recipient?.Phone);

        var check = Assert.Single(StubHttpServer.ReceivedBodies(CodeCheckStub.VerifyPath));

        Assert.Equal(UserMessages.LoginCodeAction, $"{check["action"]}");
        Assert.Equal(stepUpEntity.Id.ToString(), $"{check["correlationId"]}");

        var ticket = $"{stepUp.Body["ticket"]}";
        var expiresAt = ((DateTime?)stepUp.Body["expiresAt"])?.ToUniversalTime();

        Assert.Equal(HttpStatusCode.OK, stepUp.Response.StatusCode);
        Assert.False(string.IsNullOrEmpty(ticket));
        Assert.NotNull(stepUpEntity.Ticket);
        Assert.NotEqual(ticket, stepUpEntity.Ticket);
        Assert.NotNull(expiresAt);
        Assert.InRange(expiresAt.Value, DateTime.UtcNow.AddMinutes(4), DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(HttpStatusCode.OK, deletion.Response.StatusCode);
        Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(targetUserId));

        var data = (await FindDeletionNoticeAsync(targetUserId))?.GetData<UserMessageData>();

        Assert.NotNull(data?.DeletedAt);
        Assert.Null(data?.Device);
        Assert.Null(data?.IpAddress);
    }

    [Fact]
    public async Task One_Authenticator_Code_Confirms_Several_Deletions_Until_The_Ticket_Expires()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        await AllowEmailTwoFactorAsync();

        var key = await CreateAuthenticatorKeyAsync();
        var accessToken = await LoginAsync();

        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Authenticator);

        var firstTargetId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        var code = await SendAsync(accessToken, HttpMethod.Post, "/step-up/code");
        var stepUp = await SendAsync(accessToken, HttpMethod.Post, "/step-up", new { Code = TotpCodes.For(key) });
        var ticket = $"{stepUp.Body["ticket"]}";

        // Act
        var first = await SendAsync(accessToken, HttpMethod.Delete, $"/{firstTargetId}", new { Ticket = ticket });
        var secondTargetId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        var second = await SendAsync(accessToken, HttpMethod.Delete, $"/{secondTargetId}", new { Ticket = ticket });

        TestDbHelper.ExpireStepUpEntities(UserSettingHelper.UserId);

        var thirdTargetId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        var third = await SendAsync(accessToken, HttpMethod.Delete, $"/{thirdTargetId}", new { Ticket = ticket });

        // Assert
        Assert.Equal("Authenticator", $"{code.Body["method"]}");
        Assert.Null(code.Body["codeSentTo"]);
        Assert.Empty(StubHttpServer.Requests);
        Assert.Equal(HttpStatusCode.OK, first.Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.Response.StatusCode);
        Assert.Equal(HttpStatusCode.NotAcceptable, third.Response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, $"{third.Body["error"]?["message"]}");
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(thirdTargetId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-ticket")]
    public async Task An_Admin_Deletion_Without_A_Valid_Ticket_Asks_For_The_Sign_In_Code_First(string? ticket)
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Delete, $"/{targetUserId}", new { Ticket = ticket });

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, $"{response.Body["error"]?["message"]}");
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(targetUserId));
    }

    [Fact]
    public async Task A_Wrong_Step_Up_Code_Counts_Like_A_Failed_Second_Step()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();
        await AllowEmailTwoFactorAsync();

        var accessToken = await LoginAsync();

        TestDbHelper.SetTwoFactorMethod(UserSettingHelper.UserId, TwoFactorMethod.Email);
        StubHttpServer.AnswerVerify("WrongCode", 4);

        await SendAsync(accessToken, HttpMethod.Post, "/step-up/code");

        // Act
        var response = await SendAsync(accessToken, HttpMethod.Post, "/step-up", new { Code = "000000" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Codes.NotAccepted, $"{response.Body["error"]?["message"]}");
        Assert.Equal(1, TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId)?.AccessFailedCount);
        Assert.Equal(1, TestDbHelper.CountAccountLimitEvents(UserSettingHelper.UserId, AccountLimitKind.FailedSecondStep));
        Assert.Null(Assert.Single(TestDbHelper.GetStepUpEntitiesWithNoTracking(UserSettingHelper.UserId)).Ticket);
    }

    [Fact]
    public async Task An_Admin_Without_Two_Step_Sign_In_Is_Asked_To_Set_It_Up()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var accessToken = await LoginAsync();

        // Act
        var code = await SendAsync(accessToken, HttpMethod.Post, "/step-up/code");
        var stepUp = await SendAsync(accessToken, HttpMethod.Post, "/step-up", new { Code = "123456" });

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, code.Response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.SetUpToConfirm, $"{code.Body["error"]?["message"]}");
        Assert.Equal(HttpStatusCode.NotAcceptable, stepUp.Response.StatusCode);
        Assert.Equal(ErrorMessages.TwoFactor.SetUpToConfirm, $"{stepUp.Body["error"]?["message"]}");
    }

    [Fact]
    public async Task A_User_Without_An_Admin_Role_Cant_Step_Up()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await SendAsUserAsync(HttpMethod.Post, "/step-up/code");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.Response.StatusCode);
    }

    [Fact]
    public async Task An_App_Admin_Cant_Delete_An_Admin_Account()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.AdminRoles.First(), targetUserId);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Delete, $"/{targetUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(targetUserId));
    }

    [Fact]
    public async Task The_Last_Global_Admin_Cant_Delete_Their_Account()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await SendAsUserAsync(
            HttpMethod.Delete,
            $"/{UserSettingHelper.UserId}",
            new { Password = UserSettingHelper.UserPassword }
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotAcceptable, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Users.LastGlobalAdmin, $"{response.Body["error"]?["message"]}");
        Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    }

    private Task<MessageEvent?> FindDeletionNoticeAsync(string userId) =>
        NatsTestHelper.FindMessageAsync(
            NatsTestHelper.UserDeletedSubject,
            message => message.CorrelationId == userId && message.OccurredAt >= TestStartedAt.AddSeconds(-1)
        );

    private async Task AllowEmailTwoFactorAsync() =>
        await _testingWebAppFactory.UpdateSignInSettingsAsync(
            TestingWebAppFactory.TestAppName,
            new UpdateSignInSettingsCommand
            {
                RequireConfirmedAccount = true,
                DefaultRole = TestingWebAppFactory.DefaultRole,
                AllowAuthenticatorTwoFactor = true,
                AllowEmailTwoFactor = true,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<string> CreateAuthenticatorKeyAsync()
    {
        var userManager = ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByIdAsync(UserSettingHelper.UserId);

        Assert.True((await userManager.ResetAuthenticatorKeyAsync(user!)).Succeeded);

        return (await userManager.GetAuthenticatorKeyAsync(user!))!;
    }

    private async Task<string> LoginAsync()
    {
        (var accessToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        return accessToken;
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> SendAsUserAsync(HttpMethod method, string path, object? body = null) =>
        await SendAsync(await LoginAsync(), method, path, body);

    private async Task<(HttpResponseMessage Response, JObject Body)> SendAsync(
        string accessToken,
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(method, BaseUri + path);
        httpRequest.AddAuthHeader(accessToken);
        httpRequest.Headers.UserAgent.ParseAdd(Device);

        if (body is not null)
            httpRequest.Content = SerDeHelper.ConvertToHttpContent(body);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
