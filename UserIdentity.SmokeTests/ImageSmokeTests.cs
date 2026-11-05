using System.Net;
using System.Text.Json.Nodes;

using Xunit;

namespace UserIdentity.SmokeTests;

public class ImageSmokeTests(ImageFixture fixture) : IClassFixture<ImageFixture>
{
  private const string ProductScopeResource = "commercemanager-product";

  // The cleanups run on a one-minute timer
  private static readonly TimeSpan _cleanupWait = TimeSpan.FromSeconds(90);
  private static readonly TimeSpan _messageEffectWait = TimeSpan.FromSeconds(30);
  private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

  private readonly ImageFixture _fixture = fixture;
  private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

  [Fact]
  public async Task KeySet_IsPublishedWithoutAnApiKey()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl, withApiKey: false);

    // Act
    var response = await client.GetAsync("/api/v1/jwks/keys", _cancellationToken);

    // Assert
    var body = await ServiceClient.ReadAsync(response, HttpStatusCode.OK, _cancellationToken);
    Assert.Equal("Ed25519", $"{body["keys"]![0]!["crv"]}");
    Assert.NotEmpty($"{body["keys"]![0]!["x"]}");
  }

  [Fact]
  public async Task Requests_AreRejectedInTheErrorShape()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);
    using var clientWithoutKey = new ServiceClient(_fixture.ServiceUrl, withApiKey: false);

    // Act
    var noApiKey = await clientWithoutKey.PostAsync("/api/v1/user/login", new { }, _cancellationToken);
    var noToken = await client.GetAsync("/api/v1/role", _cancellationToken);
    var unreadable = await client.PostRawAsync("/api/v1/user/login", "{", _cancellationToken);
    var missingFields = await client.PostAsync("/api/v1/user/register", new { }, _cancellationToken);

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, noApiKey.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);

    var unreadableBody = await ServiceClient.ReadAsync(unreadable, HttpStatusCode.BadRequest, _cancellationToken);
    Assert.Equal("The request couldn't be read.", $"{unreadableBody["error"]!["errorList"]![0]!["message"]}");

    var missingFieldsBody = await ServiceClient.ReadAsync(
      missingFields,
      HttpStatusCode.BadRequest,
      _cancellationToken);
    Assert.Contains(
      missingFieldsBody["error"]!["errorList"]!.AsArray(),
      error => $"{error!["message"]}" == "The FirstName field is required.");
  }

  [Fact]
  public async Task SignUp_ConfirmedByMessage_StartsASessionThatRefreshesAndEnds()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);

    // Act
    var user = await client.RegisterAsync(_cancellationToken);
    var welcome = await _fixture.FindMessageAsync(ImageFixture.UserUpdatedSubject, user.Id, _cancellationToken);
    var sessionBeforeConfirming = await client.LogInAsync(user.UserName, ServiceClient.Password, _cancellationToken);

    await ConfirmAsync(client, user);

    var session = (await client.LogInAsync(user.UserName, ServiceClient.Password, _cancellationToken))!;
    client.SignIn(session.AccessToken);
    var profile = await client.GetAsync($"/api/v1/user/{user.Id}", _cancellationToken);

    var refreshed = await client.RefreshAsync(session, _cancellationToken);
    client.SignIn(refreshed.AccessToken);
    var profileAfterRefresh = await client.GetAsync($"/api/v1/user/{user.Id}", _cancellationToken);

    var logout = await client.PostAsync(
      "/api/v1/user/logout",
      new { refreshToken = refreshed.RefreshToken },
      _cancellationToken);
    var refreshAfterLogout = await client.PostAsync(
      "/api/v1/user/refresh-token",
      new { accessToken = refreshed.AccessToken, refreshToken = refreshed.RefreshToken },
      _cancellationToken);

    // Assert
    Assert.Equal("WelcomeUser", $"{welcome["action"]}");
    Assert.Equal(user.Email, $"{welcome["attributes"]!["RecipientEmail"]}");
    Assert.Null(sessionBeforeConfirming);

    var profileBody = await ServiceClient.ReadAsync(profile, HttpStatusCode.OK, _cancellationToken);
    Assert.Equal("Smoke", $"{profileBody["user"]!["firstName"]}");

    Assert.NotEqual(session.RefreshToken, refreshed.RefreshToken);
    Assert.Equal(HttpStatusCode.OK, profileAfterRefresh.StatusCode);
    Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
  }

  [Fact]
  public async Task PasswordReset_VerifiedByMessage_ChangesThePassword()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);
    const string newPassword = "N3wP@ssW1rd";
    var user = await CreateConfirmedUserAsync(client);

    // Act
    var resetResponse = await client.PostAsync(
      "/api/v1/user/reset-password",
      new { userName = user.UserName },
      _cancellationToken);
    var resetBody = await ServiceClient.ReadAsync(resetResponse, HttpStatusCode.OK, _cancellationToken);
    var resetRequestId = $"{resetBody["resetPasswordDetails"]!["resetRequestId"]}";
    var message = await _fixture.FindMessageAsync(ImageFixture.UserUpdatedSubject, resetRequestId, _cancellationToken);

    await _fixture.PublishCodeVerifiedAsync(resetRequestId, "PasswordReset", _cancellationToken);

    var updated = await WaitUntilAsync(
      async () => (await client.PostAsync(
        "/api/v1/user/update-password",
        new { resetRequestId, newPassword },
        _cancellationToken)).StatusCode == HttpStatusCode.OK,
      _messageEffectWait);

    var oldPassword = await client.PostAsync(
      "/api/v1/user/login",
      new { userName = user.UserName, password = ServiceClient.Password },
      _cancellationToken);
    var session = await client.LogInAsync(user.UserName, newPassword, _cancellationToken);

    // Assert
    Assert.Equal("PasswordReset", $"{message["action"]}");
    Assert.True(updated);
    Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
    Assert.NotNull(session);
  }

  [Fact]
  public async Task AppAdmin_ManagesRolesScopesInvitesAndTheWaitList()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);
    var member = await client.RegisterAsync(_cancellationToken);
    await SignInWithRoleAsync(client, $"{ServiceClient.AppName}:admin");

    var roleName = $"{ServiceClient.AppName}:{Guid.NewGuid():N}";
    var inviteEmail = $"{Guid.NewGuid():N}@example.com";
    var waitListEmail = $"{Guid.NewGuid():N}@example.com";

    // Act
    var role = await ReadCreatedAsync(client, "/api/v1/role", new { roleName });
    var roleId = $"{role["role"]!["id"]}";

    await ReadCreatedAsync(
      client,
      "/api/v1/role/claim",
      new { roleId, resource = ProductScopeResource, action = "read" });

    var memberRoles = await ReadCreatedAsync(client, "/api/v1/role/user", new { userId = member.Id, roleId });
    var roles = await ReadOkAsync(client, "/api/v1/role");
    var claims = await ReadOkAsync(client, $"/api/v1/role/claim/{roleId}");

    var invite = await ReadCreatedAsync(
      client,
      "/api/v1/invite-code",
      new { userEmail = inviteEmail, inviteCode = "SMOKEINVITE1", role = roleName });
    var invites = await ReadOkAsync(client, $"/api/v1/invite-code?search={inviteEmail}&applied=false&pageSize=5");
    var revoked = await client.DeleteAsync($"/api/v1/invite-code/{invite["inviteCode"]!["id"]}", _cancellationToken);

    await ReadCreatedAsync(client, "/api/v1/wait-list", new { userEmail = waitListEmail });
    var waitList = await ReadOkAsync(client, $"/api/v1/wait-list/email/{waitListEmail}");

    var appEntities = await ReadOkAsync(client, "/api/v1/app-entity");

    // Assert
    Assert.Contains(memberRoles["userRoles"]!.AsArray(), name => $"{name}" == roleName);
    Assert.Contains(roles["roles"]!.AsArray(), item => $"{item!["name"]}" == roleName);
    Assert.Equal($"{ProductScopeResource}:read", $"{claims["roleClaims"]![0]!["scope"]}");
    Assert.Equal(1, invites["totalCount"]!.GetValue<int>());
    Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
    Assert.Equal(waitListEmail, $"{waitList["waitList"]!["userEmail"]}");
    Assert.NotEmpty(appEntities["appEntities"]!.AsArray());
  }

  [Fact]
  public async Task GlobalAdmin_ReadsTheRegisteredApps()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);
    await SignInWithRoleAsync(client, "global:admin");

    // Act
    var registeredApps = await ReadOkAsync(client, "/api/v1/registered-app");

    // Assert
    Assert.Contains(
      registeredApps["registeredApps"]!.AsArray(),
      app => string.Equals($"{app!["appName"]}", ServiceClient.AppName, StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  public async Task Cleanups_DeleteExpiredRowsAndUnconfirmedUsers()
  {
    // Arrange
    using var client = new ServiceClient(_fixture.ServiceUrl);
    var user = await CreateConfirmedUserAsync(client);
    var unconfirmed = await client.RegisterAsync(_cancellationToken);

    await client.PostAsync("/api/v1/user/reset-password", new { userName = user.UserName }, _cancellationToken);

    await _fixture.RunSqlAsync(
      $"""
      UPDATE refresh_tokens SET expires = '2000-01-01' WHERE user_id = '{user.Id}';
      UPDATE password_resets SET expires = '2000-01-01' WHERE user_id = '{user.Id}';
      UPDATE user_details SET created_at = '2000-01-01' WHERE id = '{unconfirmed.Id}';
      """,
      _cancellationToken);

    // Act
    var removed = await WaitUntilAsync(
      async () => await _fixture.RunSqlAsync(
        $"""
        SELECT (SELECT COUNT(*) FROM refresh_tokens WHERE user_id = '{user.Id}')
          + (SELECT COUNT(*) FROM password_resets WHERE user_id = '{user.Id}')
          + (SELECT COUNT(*) FROM users WHERE id = '{unconfirmed.Id}') AS remaining;
        """,
        _cancellationToken) == "remaining\n0",
      _cleanupWait);

    // Assert
    Assert.True(removed);
  }

  private async Task<SmokeUser> CreateConfirmedUserAsync(ServiceClient client)
  {
    var user = await client.RegisterAsync(_cancellationToken);

    await ConfirmAsync(client, user);

    return user;
  }

  private async Task ConfirmAsync(ServiceClient client, SmokeUser user)
  {
    await _fixture.PublishCodeVerifiedAsync(user.Id, "WelcomeUser", _cancellationToken);

    var confirmed = await WaitUntilAsync(
      async () => await client.LogInAsync(user.UserName, ServiceClient.Password, _cancellationToken) is not null,
      _messageEffectWait);

    Assert.True(confirmed);
  }

  private async Task SignInWithRoleAsync(ServiceClient client, string roleName)
  {
    var user = await CreateConfirmedUserAsync(client);

    await _fixture.GrantRoleAsync(user.UserName, roleName, _cancellationToken);

    var session = await client.LogInAsync(user.UserName, ServiceClient.Password, _cancellationToken);

    client.SignIn(session!.AccessToken);
  }

  private async Task<JsonNode> ReadOkAsync(ServiceClient client, string path) =>
    await ServiceClient.ReadAsync(
      await client.GetAsync(path, _cancellationToken),
      HttpStatusCode.OK,
      _cancellationToken);

  private async Task<JsonNode> ReadCreatedAsync(ServiceClient client, string path, object body) =>
    await ServiceClient.ReadAsync(
      await client.PostAsync(path, body, _cancellationToken),
      HttpStatusCode.Created,
      _cancellationToken);

  private async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
  {
    var deadline = DateTime.UtcNow + timeout;

    while (DateTime.UtcNow < deadline)
    {
      if (await condition())
        return true;

      await Task.Delay(_pollInterval, _cancellationToken);
    }

    return false;
  }
}
