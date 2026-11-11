using FakeItEasy;
using Microsoft.AspNetCore.Http;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Interfaces;
using UserIdentity.Presentation.Filters;
using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class GoogleRecaptchaFilterTests
{
    private readonly IGoogleRecaptchaService _googleRecaptchaService = A.Fake<IGoogleRecaptchaService>();

    [Fact]
    public async Task InvokeAsync_With_A_Valid_Token_Runs_The_Rest_Of_The_Endpoint()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var context = EndpointFilterInvocationContext.Create(httpContext, GetCommand("recaptcha-token"));

        A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync("recaptcha-token", httpContext.RequestAborted)).Returns(true);

        // Act
        var result = await new GoogleRecaptchaFilter(_googleRecaptchaService).InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>("endpoint result")
        );

        // Assert
        Assert.Equal("endpoint result", result);
    }

    [Fact]
    public async Task InvokeAsync_With_A_Failed_Token_Throws_InvalidDataException_Without_Running_The_Endpoint()
    {
        // Arrange
        var context = EndpointFilterInvocationContext.Create(new DefaultHttpContext(), GetCommand("recaptcha-token"));
        var endpointRan = false;

        A.CallTo(() => _googleRecaptchaService.VerifyTokenAsync("recaptcha-token", A<CancellationToken>._)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await new GoogleRecaptchaFilter(_googleRecaptchaService).InvokeAsync(
                context,
                _ =>
                {
                    endpointRan = true;
                    return ValueTask.FromResult<object?>(null);
                }
            )
        );

        Assert.Equal("Invalid GoogleRecaptchaToken provided", exception.Message);
        Assert.False(endpointRan);
    }

    private static RegisterUserCommand GetCommand(string googleRecaptchaToken) =>
        new()
        {
            FirstName = "Test",
            UserName = "test.user",
            UserEmail = "test.user@domain.com",
            UserPassword = "Password123",
            GoogleRecaptchaToken = googleRecaptchaToken,
        };
}
