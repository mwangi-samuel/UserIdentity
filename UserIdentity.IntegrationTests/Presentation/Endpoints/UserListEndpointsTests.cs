using System.Net;
using Newtonsoft.Json.Linq;
using UserIdentity.Common.Errors;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class UserListEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string BaseUri = "/api/v1/user";

    [Fact]
    public async Task An_App_Admin_Lists_The_Apps_Users_Newest_First_With_Roles_Activity_And_Delete_Rights()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var otherAppUserId = TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName, "second.app.user");
        var appUserId = CreateUserOfTestApp();
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.DefaultRole, appUserId);
        TestDbHelper.CreateRefreshTokenEntity(appUserId);

        // Act
        var response = await GetAsync("?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal(2, response.Body["totalCount"]?.Value<int>());

        var users = response.Body["users"]?.ToObject<List<JObject>>() ?? [];

        Assert.Equal([appUserId, UserSettingHelper.UserId], users.Select(user => $"{user["id"]}"));
        Assert.DoesNotContain(users, user => $"{user["id"]}" == otherAppUserId);

        var appUser = users[0];
        var admin = users[1];

        Assert.Equal([TestDbHelper.DefaultRole], appUser["roles"]?.ToObject<List<string>>());
        Assert.NotNull(appUser["lastActiveAt"]);
        Assert.False(appUser["emailConfirmed"]?.Value<bool>());
        Assert.False(appUser["lockedOut"]?.Value<bool>());
        Assert.True(appUser["deleteEnabled"]?.Value<bool>());
        Assert.Contains(TestDbHelper.AdminRoles.First(), admin["roles"]?.ToObject<List<string>>() ?? []);
        Assert.True(admin["emailConfirmed"]?.Value<bool>());
        Assert.False(admin["deleteEnabled"]?.Value<bool>());
    }

    [Fact]
    public async Task The_Filters_Apply_Before_Counting()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        var appUserId = CreateUserOfTestApp();
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.DefaultRole, appUserId);

        // Act
        var bySearch = await GetAsync("?search=OTHER.APP");
        var byRole = await GetAsync($"?role={TestDbHelper.AdminRoles.First()}");
        var byConfirmation = await GetAsync("?emailConfirmed=false");

        // Assert
        Assert.Equal([appUserId], UserIds(bySearch.Body));
        Assert.Equal([UserSettingHelper.UserId], UserIds(byRole.Body));
        Assert.Equal([appUserId], UserIds(byConfirmation.Body));
        Assert.Equal(1, byConfirmation.Body["totalCount"]?.Value<int>());
    }

    [Fact]
    public async Task A_Role_Of_Another_App_Is_Refused()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsAdmin();

        // Act
        var response = await GetAsync("?role=otherapp:user");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.Response.StatusCode);
        Assert.Equal(ErrorMessages.Roles.NameOutsideApp("testapp:"), $"{response.Body["error"]?["message"]}");
    }

    [Fact]
    public async Task A_Global_Admin_Lists_The_App_Named_In_The_Header()
    {
        // Arrange
        TestDbHelper.SeedDatabase();
        TestDbHelper.ConfigureIdentityUserAsGlobalAdmin();

        var otherAppUserId = TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        // Act
        var response = await GetAsync("", TestingWebAppFactory.OtherAppName);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.Response.StatusCode);
        Assert.Equal([otherAppUserId], UserIds(response.Body));
        Assert.True(response.Body["users"]?[0]?["deleteEnabled"]?.Value<bool>());
    }

    [Fact]
    public async Task A_User_Without_An_Admin_Role_Cant_List_Users()
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        var response = await GetAsync("");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.Response.StatusCode);
    }

    private string CreateUserOfTestApp() => TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);

    private static List<string> UserIds(JObject body) => [.. body["users"]?.Select(user => $"{user["id"]}") ?? []];

    private async Task<(HttpResponseMessage Response, JObject Body)> GetAsync(
        string query,
        string appName = TestingWebAppFactory.TestAppName
    )
    {
        var request = await HttpClient.CreateAuthorizedHttpRequestMessageAsync(HttpMethod.Get, BaseUri + query, appName);
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        return (response, SerDeHelper.Deserialize<JObject>(await response.ValidateRequestResponseAsync()) ?? []);
    }
}
