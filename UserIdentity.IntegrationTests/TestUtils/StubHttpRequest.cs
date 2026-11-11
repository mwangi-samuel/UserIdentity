namespace UserIdentity.IntegrationTests.TestUtils;

public sealed record StubHttpRequest(
    string Path,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Headers,
    string Body
);
