using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace UserIdentity.SmokeTests;

public sealed record SmokeUser(string Id, string UserName, string Email);

public sealed record SmokeSession(string AccessToken, string RefreshToken);

public sealed class ServiceClient : IDisposable
{
    public const string AppName = "testapp";
    public const string Password = "P@ssW1rd";

    private readonly HttpClient _httpClient;

    public ServiceClient(Uri serviceUrl, bool withApiKey = true)
    {
        _httpClient = new HttpClient { BaseAddress = serviceUrl };
        _httpClient.DefaultRequestHeaders.Add("X-App-Name", AppName);

        if (withApiKey)
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", ImageFixture.ApiKey);
    }

    public void SignIn(string accessToken) =>
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken) =>
        _httpClient.GetAsync(path, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string path, CancellationToken cancellationToken) =>
        _httpClient.DeleteAsync(path, cancellationToken);

    public Task<HttpResponseMessage> PostAsync(string path, object body, CancellationToken cancellationToken) =>
        _httpClient.PostAsJsonAsync(path, body, cancellationToken);

    public Task<HttpResponseMessage> PostRawAsync(string path, string body, CancellationToken cancellationToken) =>
        _httpClient.PostAsync(path, new StringContent(body, new MediaTypeHeaderValue("application/json")), cancellationToken);

    public async Task<SmokeUser> RegisterAsync(CancellationToken cancellationToken)
    {
        var userName = $"smoke{Guid.NewGuid():N}";
        var email = $"{userName}@example.com";

        var response = await PostAsync(
            "/api/v1/user/register",
            new
            {
                firstName = "Smoke",
                lastName = "Test",
                userName,
                userEmail = email,
                userPassword = Password,
            },
            cancellationToken
        );

        var body = await ReadAsync(response, HttpStatusCode.Created, cancellationToken);

        return new SmokeUser($"{body["user"]!["id"]}", userName, email);
    }

    public async Task<SmokeSession?> LogInAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var response = await PostAsync("/api/v1/user/login", new { userName, password }, cancellationToken);
        var body = await ReadAsync(response, HttpStatusCode.OK, cancellationToken);

        return body["isConfirmed"]!.GetValue<bool>() ? ReadSession(body["userToken"]!) : null;
    }

    public async Task<SmokeSession> RefreshAsync(SmokeSession session, CancellationToken cancellationToken)
    {
        var response = await PostAsync(
            "/api/v1/user/refresh-token",
            new { accessToken = session.AccessToken, refreshToken = session.RefreshToken },
            cancellationToken
        );

        return ReadSession((await ReadAsync(response, HttpStatusCode.OK, cancellationToken))["userToken"]!);
    }

    public static async Task<JsonNode> ReadAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken
    )
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return response.StatusCode == expectedStatusCode
            ? JsonNode.Parse(content)!
            : throw new InvalidOperationException($"{response.RequestMessage?.RequestUri}: {response.StatusCode} {content}");
    }

    public void Dispose() => _httpClient.Dispose();

    private static SmokeSession ReadSession(JsonNode userToken) =>
        new($"{userToken["accessToken"]!["token"]}", $"{userToken["refreshToken"]}");
}
