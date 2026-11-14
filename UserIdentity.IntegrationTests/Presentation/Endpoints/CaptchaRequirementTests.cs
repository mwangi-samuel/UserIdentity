using System.Net;
using Newtonsoft.Json.Linq;
using PolyzenKit.Common.Utilities;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class CaptchaRequirementTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private const string RegisterUri = "/api/v1/user/register";
    private const string ResetPasswordUri = "/api/v1/user/reset-password";
    private const string WaitListUri = "/api/v1/wait-list";
    private const string CaptchaToken = "captcha-token";

    [Theory]
    [InlineData(RegisterUri, "register")]
    [InlineData(ResetPasswordUri, "reset-password")]
    [InlineData(WaitListUri, "wait-list")]
    public async Task Endpoint_Checks_The_Captcha_Token_For_Its_Action(string uri, string action)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        // Act
        await SendAsync(uri);

        // Assert
        Assert.Equal([new CaptchaCheck(CaptchaToken, action)], CaptchaVerifier.Checks);
    }

    [Theory]
    [InlineData(RegisterUri, "register")]
    [InlineData(ResetPasswordUri, "reset-password")]
    [InlineData(WaitListUri, "wait-list")]
    public async Task Endpoint_Refuses_A_Failed_Captcha_Check(string uri, string action)
    {
        // Arrange
        TestDbHelper.SeedDatabase();

        CaptchaVerifier.Passes = false;

        // Act
        var response = await SendAsync(uri);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);
        Assert.Equal("The captcha check failed", $"{jsonObject["error"]?["message"]}");
        Assert.Equal(action, Assert.Single(CaptchaVerifier.Checks).Action);
        Assert.Equal(0, TestDbHelper.CountOutboxMessages());
        Assert.Null(TestDbHelper.GetWaitListEntityByEmail(UserSettingHelper.UserEmail));
    }

    private Task<HttpResponseMessage> SendAsync(string uri)
    {
        var request = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, uri);

        request.Headers.Add(CustomHeaderNames.CaptchaToken, CaptchaToken);
        request.Content = SerDeHelper.ConvertToHttpContent(BodyFor(uri));

        return HttpClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static object BodyFor(string uri) =>
        uri switch
        {
            RegisterUri => new
            {
                UserSettingHelper.FirstName,
                UserName = "captcha.user",
                UserEmail = "captcha.user@example.com",
                UserSettingHelper.UserPassword,
            },
            ResetPasswordUri => new { UserSettingHelper.UserName },
            _ => new { UserSettingHelper.UserEmail },
        };
}
