using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class StubHttpServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentDictionary<string, Func<StubHttpRequest, IResult>> _responders = new();
    private readonly ConcurrentQueue<StubHttpRequest> _requests = new();

    public StubHttpServer()
    {
        var builder = WebApplication.CreateEmptyBuilder(new());
        builder.WebHost.UseKestrelCore();

        _app = builder.Build();
        _app.Urls.Add("http://127.0.0.1:0");
        _app.Run(RespondAsync);
    }

    public string Url => _app.Urls.Single();

    public IReadOnlyList<StubHttpRequest> Requests => [.. _requests];

    public Task StartAsync(CancellationToken cancellationToken) => _app.StartAsync(cancellationToken);

    public void OnPost(string path, Func<StubHttpRequest, IResult> respond) => _responders[path] = respond;

    public void Reset()
    {
        _responders.Clear();
        _requests.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private async Task RespondAsync(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);

        var request = new StubHttpRequest(
            context.Request.Path,
            context.Request.Query.ToDictionary(parameter => parameter.Key, parameter => $"{parameter.Value}"),
            context.Request.Headers.ToDictionary(header => header.Key, header => $"{header.Value}", StringComparer.OrdinalIgnoreCase),
            await reader.ReadToEndAsync(context.RequestAborted)
        );

        _requests.Enqueue(request);

        var result =
            HttpMethods.IsPost(context.Request.Method) && _responders.TryGetValue(request.Path, out var respond)
                ? respond(request)
                : Results.NotFound();

        await result.ExecuteAsync(context);
    }
}
