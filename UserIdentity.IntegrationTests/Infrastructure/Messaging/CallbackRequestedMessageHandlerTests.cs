using System.Net;
using System.Text;
using System.Text.Json;

using Newtonsoft.Json.Linq;

using PolyzenKit.Common.Utilities;

using UserIdentity.IntegrationTests.TestUtils;

using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

using Xunit;

namespace UserIdentity.IntegrationTests.Infrastructure.Messaging;

public class CallbackRequestedMessageHandlerTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
  ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private const string RecaptchaToken = "callback-recaptcha-token";

  private readonly WireMockServer _wireMock = testingWebAppFactory.WireMockServer;

  [Fact]
  public async Task Sign_Up_Through_The_Apps_UI_Sends_One_Signed_Callback()
  {
    // Arrange
    SetUpWireMock(failFirstCallback: false);

    // Act
    var userId = await RegisterAsync(TestingWebAppFactory.CallbackAppName);

    // Assert
    var callbacks = await WaitForCallbacksAsync(1);
    var callback = Assert.Single(callbacks).RequestMessage;
    Assert.NotNull(callback);

    using var body = JsonDocument.Parse(callback.Body!);
    Assert.Equal(userId, body.RootElement.GetProperty("userContent").GetProperty("userIdentityId").GetString());
    Assert.Equal("Create", body.RootElement.GetProperty("eventType").GetString());

    var timestamp = callback.Headers![CustomHeaderNames.Timestamp].Single();
    var signature = callback.Headers[CustomHeaderNames.Signature].Single();
    Assert.True(
      HmacSignature.IsValid(
        TestingWebAppFactory.CallbackAppSecretKey,
        timestamp,
        Encoding.UTF8.GetBytes(callback.Body!),
        signature,
        DateTimeOffset.UtcNow,
        TimeSpan.FromMinutes(5)));
    Assert.Equal("TestApiKey", callback.Headers["X-Api-Key"].Single());
  }

  [Fact]
  public async Task A_Failed_Callback_Is_Sent_Again()
  {
    // Arrange
    SetUpWireMock(failFirstCallback: true);

    // Act
    await RegisterAsync(TestingWebAppFactory.CallbackAppName);

    // Assert
    var callbacks = await WaitForCallbacksAsync(2);

    Assert.Equal(2, callbacks.Count);
    var first = callbacks[0].RequestMessage;
    var second = callbacks[1].RequestMessage;
    Assert.NotNull(first);
    Assert.NotNull(second);
    Assert.True(second.DateTime - first.DateTime >= TimeSpan.FromMilliseconds(800));
  }

  [Fact]
  public async Task Sign_Up_For_An_App_Without_A_Callback_Url_Adds_No_Callback_Message()
  {
    // Arrange
    SetUpWireMock(failFirstCallback: false);

    // Act
    var userId = await RegisterAsync(RegisteredApp.AppName);

    // Assert
    Assert.NotNull(await NatsTestHelper.FindMessageAsync(NatsTestHelper.UserUpdatedSubject, userId));

    await PollyResiliencePipeline.ExecuteAsync(token =>
    {
      Assert.Equal(0, TestDbHelper.CountOutboxMessages());
      return ValueTask.CompletedTask;
    }, TestContext.Current.CancellationToken);

    Assert.Null(
      await NatsTestHelper.FindMessageAsync(NatsTestHelper.CallbackRequestedSubject, userId, TimeSpan.FromSeconds(2)));
    Assert.Empty(CallbackLogEntries());
  }

  private void SetUpWireMock(bool failFirstCallback)
  {
    _wireMock.Reset();

    _wireMock.Given(Request.Create().WithPath("/siteverify").UsingPost())
      .RespondWith(
        Response.Create().WithStatusCode(HttpStatusCode.OK).WithBody("""{ "success": true, "score": 0.9 }"""));

    if (!failFirstCallback)
    {
      _wireMock.Given(Request.Create().WithPath(TestingWebAppFactory.CallbackPath).UsingPost())
        .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));

      return;
    }

    _wireMock.Given(Request.Create().WithPath(TestingWebAppFactory.CallbackPath).UsingPost())
      .InScenario("callback-retry")
      .WillSetStateTo("failed-once")
      .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.InternalServerError));

    _wireMock.Given(Request.Create().WithPath(TestingWebAppFactory.CallbackPath).UsingPost())
      .InScenario("callback-retry")
      .WhenStateIs("failed-once")
      .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.OK));
  }

  private async Task<string> RegisterAsync(string appName)
  {
    var request = ApiRequestHelper.CreateHttpRequestMessage(HttpMethod.Post, "/api/v1/user/register");
    request.Headers.Remove("X-App-Name");
    request.Headers.Add("X-App-Name", appName);
    request.Content = SerDeHelper.ConvertToHttpContent(new
    {
      UserSettingHelper.FirstName,
      UserSettingHelper.UserName,
      UserSettingHelper.UserEmail,
      UserSettingHelper.UserPassword,
      GoogleRecaptchaToken = RecaptchaToken
    });

    var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);
    var responseString = await response.ValidateRequestResponseAsync();

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    return SerDeHelper.Deserialize<JObject>(responseString)?["user"]?["id"]?.ToString()
      ?? throw new InvalidOperationException("The sign-up returned no user id");
  }

  private List<ILogEntry> CallbackLogEntries() =>
    [
      .. _wireMock.LogEntries.Where(entry => entry.RequestMessage?.Path == TestingWebAppFactory.CallbackPath)
        .OrderBy(entry => entry.RequestMessage?.DateTime)
    ];

  private async Task<List<ILogEntry>> WaitForCallbacksAsync(int count)
  {
    var deadline = DateTime.UtcNow.AddSeconds(30);

    while (CallbackLogEntries().Count < count && DateTime.UtcNow < deadline)
      await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

    return CallbackLogEntries();
  }
}
