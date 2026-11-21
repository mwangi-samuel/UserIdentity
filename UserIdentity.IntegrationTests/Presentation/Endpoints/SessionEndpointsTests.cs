using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;
using Newtonsoft.Json.Linq;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class SessionEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";
    private const string Device = "TestBrowser/1.0";

    private static readonly string SessionsUri = $"{BaseUri}/{UserSettingHelper.UserId}/sessions";

    [Fact]
    public async Task Two_Sign_Ins_List_Two_Sessions_With_The_Callers_Marked_Current()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var firstToken, _) = await SignInAsync();
        (var secondToken, _) = await SignInAsync();

        // Act
        var response = await SendAsync(HttpMethod.Get, SessionsUri, secondToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);

        var sessions = response.Body["sessions"]?.Children().ToList() ?? [];

        Assert.Equal(2, sessions.Count);
        Assert.Equal([$"{SessionId(firstToken)}", $"{SessionId(secondToken)}"], sessions.Select(session => $"{session["id"]}").Order());
        Assert.Equal($"{SessionId(secondToken)}", $"{Assert.Single(sessions, session => (bool?)session["current"] == true)["id"]}");
        Assert.All(sessions, session => Assert.Equal(Device, $"{session["device"]}"));
    }

    [Fact]
    public async Task Ending_Another_Session_Makes_Its_Refresh_Token_Fail_And_Keeps_The_Callers()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var first = await SignInAsync();
        var second = await SignInAsync();

        // Act
        var response = await SendAsync(HttpMethod.Delete, $"{SessionsUri}/{SessionId(first.AccessToken)}", second.AccessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshAsync(first));
        Assert.Equal(HttpStatusCode.OK, await RefreshAsync(second));
    }

    [Fact]
    public async Task Ending_A_Session_The_User_Doesnt_Have_Returns_Not_Found()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        (var accessToken, _) = await SignInAsync();

        // Act
        var response = await SendAsync(HttpMethod.Delete, $"{SessionsUri}/{Guid.CreateVersion7()}", accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.Response.StatusCode);
    }

    [Fact]
    public async Task Ending_The_Other_Sessions_Keeps_Only_The_Callers()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var first = await SignInAsync();
        var second = await SignInAsync();
        var current = await SignInAsync();

        // Act
        var response = await SendAsync(HttpMethod.Delete, SessionsUri, current.AccessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(
            [SessionId(current.AccessToken)],
            TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId).Select(token => token.Id)
        );
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshAsync(first));
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshAsync(second));
        Assert.Equal(HttpStatusCode.OK, await RefreshAsync(current));
    }

    [Fact]
    public async Task A_User_Cant_List_Another_Users_Sessions()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        var otherUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        (var accessToken, _) = await SignInAsync();

        // Act
        var response = await SendAsync(HttpMethod.Get, $"{BaseUri}/{otherUserId}/sessions", accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.Response.StatusCode);
    }

    private static Guid SessionId(string accessToken) =>
        Guid.Parse(new JsonWebToken(accessToken).GetClaim(JwtRegisteredClaimNames.Sid).Value);

    private async Task<(string AccessToken, string RefreshToken)> SignInAsync()
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, ApiRequestHelper.loginUrl);
        httpRequest.Headers.UserAgent.ParseAdd(Device);
        httpRequest.Content = SerDeHelper.ConvertToHttpContent(
            new { UserSettingHelper.UserName, Password = UserSettingHelper.UserPassword }
        );

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);
        var userToken = SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync())?["userToken"];

        var accessToken = $"{userToken?["accessToken"]?["token"]}";
        var refreshToken = $"{userToken?["refreshToken"]}";

        Assert.NotEmpty(accessToken);
        Assert.NotEmpty(refreshToken);

        return (accessToken, refreshToken);
    }

    private async Task<HttpStatusCode> RefreshAsync((string AccessToken, string RefreshToken) session)
    {
        var response = await HttpClient.SendNoAuthRequestAsync(
            HttpMethod.Post,
            $"{BaseUri}/refresh-token",
            new { session.AccessToken, session.RefreshToken }
        );

        return response.StatusCode;
    }

    private async Task<(HttpResponseMessage Response, JObject Body)> SendAsync(HttpMethod method, string uri, string accessToken)
    {
        var httpRequest = ApiRequestHelper.CreateHttpRequestMessage(method, uri);
        httpRequest.AddAuthHeader(accessToken);
        httpRequest.Headers.UserAgent.ParseAdd(Device);

        var response = await HttpClient.SendAsync(httpRequest, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
