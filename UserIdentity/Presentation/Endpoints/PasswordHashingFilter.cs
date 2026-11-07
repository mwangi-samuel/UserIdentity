using System.Threading.RateLimiting;

namespace UserIdentity.Presentation.Endpoints;

// A filter, not a global rate limiter: those run before the per-IP policy, so requests already over their
// own budget would take queue places and other clients' logins would be refused
public sealed class PasswordHashingFilter(ConcurrencyLimiter concurrencyLimiter) : IEndpointFilter
{
    private readonly ConcurrencyLimiter _concurrencyLimiter = concurrencyLimiter;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var lease = await _concurrencyLimiter.AcquireAsync(1, context.HttpContext.RequestAborted);

        return lease.IsAcquired ? await next(context) : TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
