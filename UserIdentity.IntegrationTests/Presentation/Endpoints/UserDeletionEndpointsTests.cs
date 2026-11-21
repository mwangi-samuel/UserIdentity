using System.Net;
using Newtonsoft.Json.Linq;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserDeletionEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";
    private const string Device = "TestBrowser/1.0";

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
    public async Task An_App_Admin_Deletes_An_Account_Of_The_App_Without_A_Password_And_The_Notice_Doesnt_Say_Who()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var targetUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

        // Act
        var response = await SendAsUserAsync(HttpMethod.Delete, $"/{targetUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(targetUserId));

        var data = (await FindDeletionNoticeAsync(targetUserId))?.GetData<UserMessageData>();

        Assert.NotNull(data?.DeletedAt);
        Assert.Null(data?.Device);
        Assert.Null(data?.IpAddress);
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

    private async Task<(HttpResponseMessage Response, JObject Body)> SendAsUserAsync(HttpMethod method, string path, object? body = null)
    {
        (var accessToken, _) = await HttpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(method, BaseUri + path);
        httpRequest.AddAuthHeader(accessToken);
        httpRequest.Headers.UserAgent.ParseAdd(Device);

        if (body is not null)
            httpRequest.Content = SerDeHelper.ConvertToHttpContent(body);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
