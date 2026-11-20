using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace UserIdentity.IntegrationTests.TestUtils;

public static class CodeCheckStub
{
    public const string VerifyPath = "/api/v1/message/verify";
    public const string RetryPath = "/api/v1/message/retry";

    public static void AnswerVerify(this StubHttpServer stubHttpServer, string outcome, int attemptsLeft = 0) =>
        stubHttpServer.OnPost(VerifyPath, _ => Results.Json(new { outcome, attemptsLeft }));

    public static void AnswerRetry(this StubHttpServer stubHttpServer, string outcome, int retryAfterSeconds = 0) =>
        stubHttpServer.OnPost(RetryPath, _ => Results.Json(new { outcome, retryAfterSeconds }));

    public static List<JsonNode> ReceivedBodies(this StubHttpServer stubHttpServer, string path) =>
        [.. stubHttpServer.Requests.Where(request => request.Path == path).Select(request => JsonNode.Parse(request.Body)!)];
}
