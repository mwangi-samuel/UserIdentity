using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Errors;

namespace UserIdentity.Presentation.Filters;

public sealed class GoogleRecaptchaFilter(IGoogleRecaptchaService googleRecaptchaService) : IEndpointFilter
{
    private readonly IGoogleRecaptchaService _googleRecaptchaService = googleRecaptchaService;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var command = context.Arguments.OfType<RegisterUserCommand>().Single();

        if (!await _googleRecaptchaService.VerifyTokenAsync(command.GoogleRecaptchaToken, context.HttpContext.RequestAborted))
            throw new InvalidDataException(ErrorMessages.Users.InvalidRecaptchaToken);

        return await next(context);
    }
}
