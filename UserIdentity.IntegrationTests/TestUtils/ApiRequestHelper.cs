using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using UserIdentity.Application.Core.Tokens.ViewModels;

using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class ApiRequestHelper
{

  private const string ApiKey = "TestApiKey";
  private const string AppName = "TestApp";

  public static string loginUrl = "/api/v1/user/login";

  public static async Task<(string, string)> LoginUserAsync(this HttpClient httpClient, string userName, string userPassword)
  {
    // Arrange
    var requestPayload = new
    {
      UserName = userName,
      Password = userPassword
    };

    var httpRequest = CreateHttpRequestMessage(HttpMethod.Post, loginUrl);
    httpRequest.Content = SerDeHelper.ConvertToHttpContent(requestPayload);

    var response = await httpClient.SendAsync(httpRequest);
    var responseString = await response.Content.ReadAsStringAsync();

    var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

    if (jsonObject == null)
      Assert.Fail($"Expected login to be successful but it failed {responseString}");

    var userToken = jsonObject["userToken"]?.ToObject<AccessTokenViewModel>();

    var token = userToken?.AccessToken?.Token;
    var refreshToken = userToken?.RefreshToken;

    Assert.NotNull(token);
    Assert.NotNull(refreshToken);

    return (token, refreshToken);
  }

  public static HttpRequestMessage CreateHttpRequestMessage(HttpMethod httpMethod, string uri)
  {
    var httpReuest = new HttpRequestMessage(new HttpMethod(httpMethod.Method), uri);
    httpReuest.AddXApiKey();
    httpReuest.AddXAppName();

    return httpReuest;
  }
  public static void AddAuthHeader(this HttpRequestMessage httpRequest, string authToken)
  {
    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
  }

  public static void AddXApiKey(this HttpRequestMessage httpRequest, string xApiKey)
  {
    httpRequest.Headers.Add("X-Api-Key", xApiKey);
  }

  public static void AddXApiKey(this HttpRequestMessage httpRequest)
  {
    httpRequest.AddXApiKey(ApiKey);
  }

  public static void AddXAppName(this HttpRequestMessage httpRequest)
  {
    httpRequest.Headers.Add("X-App-Name", AppName);
  }

  public static async Task<HttpRequestMessage> CreateAuthorizedHttpRequestMessageAsync(this HttpClient httpClient, HttpMethod httpMethod, string uri)
  {
    (var userToken, _) = await httpClient.LoginUserAsync(UserSettingHelper.UserName, UserSettingHelper.UserPassword);

    var httpRequest = CreateHttpRequestMessage(httpMethod, uri);

    httpRequest.AddAuthHeader(userToken);

    return httpRequest;
  }

  public static async Task<HttpResponseMessage> SendNoAuthRequestAsync(this HttpClient httpClient, HttpMethod httpMethod, string uri, object? httpBody = null)
  {
    var httpRequest = CreateHttpRequestMessage(httpMethod, uri);

    if (httpBody != null)
      httpRequest.Content = SerDeHelper.ConvertToHttpContent(httpBody);

    return await httpClient.SendAsync(httpRequest);
  }

  public static async Task<HttpResponseMessage> SendValidAuthRequestAsync(this HttpClient httpClient, HttpMethod httpMethod, string uri, object? httpBody = null)
  {
    var httpRequest = await CreateAuthorizedHttpRequestMessageAsync(httpClient, httpMethod, uri);

    if (httpBody != null)
      httpRequest.Content = SerDeHelper.ConvertToHttpContent(httpBody);

    return await httpClient.SendAsync(httpRequest);
  }

  public static async Task<HttpResponseMessage> SendInvalidAuthRequestAsync(this HttpClient httpClient, HttpMethod httpMethod, string uri, object? httpBody = null)
  {
    var httpRequest = CreateHttpRequestMessage(httpMethod, uri);

    httpRequest.AddAuthHeader(UserSettingHelper.InvalidUserToken);

    if (httpBody != null)
      httpRequest.Content = SerDeHelper.ConvertToHttpContent(httpBody);

    return await httpClient.SendAsync(httpRequest);
  }

  public static async Task<string> ValidateRequestResponseAsync(this HttpResponseMessage httpResponse)
  {
    var requestIdHeader = httpResponse.Headers.GetValues("X-Request-Id");
    Assert.NotNull(requestIdHeader);
    Assert.NotEmpty(requestIdHeader);
    Assert.Single(requestIdHeader);

    return await httpResponse.Content.ReadAsStringAsync();
  }
}
