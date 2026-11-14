using System.Collections.Concurrent;
using PolyzenKit.Application.Interfaces;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class StubCaptchaVerifier : ICaptchaVerifier
{
    private readonly ConcurrentQueue<CaptchaCheck> _checks = new();

    public bool Passes { get; set; } = true;

    public IReadOnlyList<CaptchaCheck> Checks => [.. _checks];

    public Task<bool> VerifyAsync(string? token, string action, string? remoteIp, CancellationToken cancellationToken = default)
    {
        _checks.Enqueue(new CaptchaCheck(token, action));

        return Task.FromResult(Passes);
    }

    public void Reset()
    {
        _checks.Clear();
        Passes = true;
    }
}
