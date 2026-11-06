using System.Net;
using FakeItEasy;
using Microsoft.Extensions.Options;
using PolyzenKit.Infrastructure.ExternalServices;
using UserIdentity.Infrastructure.ExternalServices;
using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.ExternalServices;

public class GoogleRecaptchaServiceTests
{
    private readonly IHttpRequestSender _httpRequestSender;
    private readonly IOptions<GoogleRecaptchaSettings> _settingsOptions;

    public GoogleRecaptchaServiceTests()
    {
        _httpRequestSender = A.Fake<IHttpRequestSender>();
        _settingsOptions = Options.Create(new GoogleRecaptchaSettings { Enabled = true, SiteKey = "site-key" });
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenDisabled_ReturnsTrue()
    {
        // Arrange
        var service = new GoogleRecaptchaService(_httpRequestSender, Options.Create(new GoogleRecaptchaSettings { Enabled = false }));

        // Act
        var result = await service.VerifyTokenAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        A.CallTo(_httpRequestSender).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task VerifyTokenAsync_WhenTokenMissing_ReturnsFalse(string? token)
    {
        // Arrange
        var service = new GoogleRecaptchaService(_httpRequestSender, _settingsOptions);

        // Act
        var result = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        A.CallTo(_httpRequestSender).MustNotHaveHappened();
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenResponseValidAndScoreHigh_ReturnsTrue()
    {
        var token = "token";
        var requestMessage = new HttpRequestMessage();
        A.CallTo(_httpRequestSender)
            .Where(call => call.Method.Name == nameof(IHttpRequestSender.CreateHttpRequestMessage))
            .WithReturnType<HttpRequestMessage>()
            .Returns(requestMessage);

        var response = new GoogleRecaptchaResponseDto { Success = true, Score = 0.9f };
        A.CallTo(_httpRequestSender)
            .Where(call => call.Method.Name == nameof(IHttpRequestSender.SendRequestAsync))
            .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
            .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(response));

        var service = new GoogleRecaptchaService(_httpRequestSender, _settingsOptions);

        var result = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);

        Assert.True(result);
        A.CallTo(() => _httpRequestSender.CreateHttpRequestMessage(HttpMethod.Post, "siteverify")).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _httpRequestSender.SendRequestAsync<GoogleRecaptchaResponseDto>(
                    "GoogleRecaptcha",
                    requestMessage,
                    HttpStatusCode.OK,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenResponseNullOrLowScore_ReturnsFalse()
    {
        var token = "token";
        var requestMessage = new HttpRequestMessage();
        A.CallTo(_httpRequestSender)
            .Where(call => call.Method.Name == nameof(IHttpRequestSender.CreateHttpRequestMessage))
            .WithReturnType<HttpRequestMessage>()
            .Returns(requestMessage);

        A.CallTo(_httpRequestSender)
            .Where(call => call.Method.Name == nameof(IHttpRequestSender.SendRequestAsync))
            .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
            .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(null));

        var service = new GoogleRecaptchaService(_httpRequestSender, _settingsOptions);

        var resultNull = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
        Assert.False(resultNull);

        var lowScoreResponse = new GoogleRecaptchaResponseDto { Success = true, Score = 0.3f };
        A.CallTo(_httpRequestSender)
            .Where(call => call.Method.Name == nameof(IHttpRequestSender.SendRequestAsync))
            .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
            .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(lowScoreResponse));

        var resultLowScore = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
        Assert.False(resultLowScore);
    }
}
