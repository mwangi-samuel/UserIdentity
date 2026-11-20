using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class RegisterForServiceTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string RegisterForServiceUri = "/api/v1/user/register-for-service";
    private const string UserEmail = "moved.user@example.com";

    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    [Fact]
    public async Task A_Request_Signed_By_The_Apps_Backend_Creates_The_Account_Without_A_Session_Ticket_Or_Message()
    {
        // Arrange
        var httpRequest = TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName);

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());
        var userId = $"{jsonObject?["user"]?["id"]}";

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(UserEmail, $"{jsonObject?["user"]?["email"]}");
        Assert.Equal("system", $"{jsonObject?["user"]?["createdBy"]}");
        Assert.Null(jsonObject?["nextStep"]);
        Assert.Null(jsonObject?["ticket"]);
        Assert.Null(jsonObject?["userToken"]);
        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(userId)?.EmailConfirmed);
        Assert.NotNull(TestDbHelper.GetIdentityUserRole(TestDbHelper.DefaultRole, userId));
        Assert.Equal(0, TestDbHelper.CountLoginTicketEntities(userId));
        Assert.Equal(0, TestDbHelper.CountOutboxMessages());
        Assert.Null(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserRegisteredSubject, userId, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task The_Backends_Frontend_Signs_In_Straight_Away_And_Confirms_The_Email_With_A_Login_Code()
    {
        // Arrange
        await HttpClient.SendAsync(
            TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName),
            TestContext.Current.CancellationToken
        );

        // Act
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            ApiRequestHelper.loginUrl,
            new { UserName = UserEmail, Password = UserSettingHelper.UserPassword }
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ConfirmEmail", $"{jsonObject?["nextStep"]}");
        Assert.Equal("m***@example.com", $"{jsonObject?["codeSentTo"]}");
        Assert.NotNull(await FindMessageSentToAsync(NatsTestHelper.LoginCodeRequestedSubject, UserEmail));
    }

    [Fact]
    public async Task A_Taken_Email_Is_Refused_Explicitly()
    {
        // Arrange
        await HttpClient.SendAsync(
            TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName),
            TestContext.Current.CancellationToken
        );

        // Act
        var response = await HttpClient.SendAsync(
            TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody("another.name"), RegisteredApp.AppName),
            TestContext.Current.CancellationToken
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal($"A record identified with - {UserEmail} - exists", $"{jsonObject?["error"]?["message"]}");
        Assert.Equal(1, TestDbHelper.CountIdentityUsers());
    }

    [Fact]
    public async Task The_Apps_Invite_Code_Rule_Still_Applies()
    {
        // Arrange
        await _testingWebAppFactory.UpdateSignInSettingsAsync(
            TestingWebAppFactory.TestAppName,
            new UpdateSignInSettingsCommand
            {
                RequireConfirmedAccount = true,
                RequireInviteCode = true,
                DefaultRole = TestingWebAppFactory.DefaultRole,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        var response = await HttpClient.SendAsync(
            TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName),
            TestContext.Current.CancellationToken
        );

        // Assert
        var jsonObject = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorMessages.Users.InviteCodeRequired, $"{jsonObject?["error"]?["message"]}");
        Assert.Equal(0, TestDbHelper.CountIdentityUsers());
    }

    [Fact]
    public async Task A_Stale_Signature_Is_Refused()
    {
        // Arrange
        var httpRequest = TestBackend.CreateSignedRequest(
            HttpMethod.Post,
            RegisterForServiceUri,
            SignUpBody(),
            RegisteredApp.AppName,
            signedAt: DateTimeOffset.UtcNow.AddMinutes(-10)
        );

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, TestDbHelper.CountIdentityUsers());
    }

    [Fact]
    public async Task A_Changed_Body_Is_Refused()
    {
        // Arrange
        var httpRequest = TestBackend.CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName);
        httpRequest.Content = new StringContent(JsonConvert.SerializeObject(SignUpBody("changed.name")), Encoding.UTF8, "application/json");

        // Act
        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, TestDbHelper.CountIdentityUsers());
    }

    [Fact]
    public async Task An_Unknown_Backend_Or_Another_Key_Is_Refused()
    {
        // Arrange
        var unknownBackend = TestBackend.CreateSignedRequest(
            HttpMethod.Post,
            RegisterForServiceUri,
            SignUpBody(),
            RegisteredApp.AppName,
            backendName: "unknownservice"
        );
        var otherKey = new TestBackend().CreateSignedRequest(HttpMethod.Post, RegisterForServiceUri, SignUpBody(), RegisteredApp.AppName);
        var otherApp = TestBackend.CreateSignedRequest(
            HttpMethod.Post,
            RegisterForServiceUri,
            SignUpBody(),
            TestingWebAppFactory.OtherAppName
        );

        // Act
        var responses = new[]
        {
            await HttpClient.SendAsync(unknownBackend, TestContext.Current.CancellationToken),
            await HttpClient.SendAsync(otherKey, TestContext.Current.CancellationToken),
            await HttpClient.SendAsync(otherApp, TestContext.Current.CancellationToken),
        };

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));
        Assert.Equal(0, TestDbHelper.CountIdentityUsers());
    }

    [Fact]
    public async Task A_User_Token_Without_A_Signature_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        // Act
        var response = await HttpClient.SendValidAuthRequestAsync(HttpMethod.Post, RegisterForServiceUri, SignUpBody());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, TestDbHelper.CountIdentityUsers());
    }

    private static object SignUpBody(string userName = UserEmail) =>
        new
        {
            UserSettingHelper.FirstName,
            UserName = userName,
            UserEmail,
            UserSettingHelper.UserPassword,
        };
}
