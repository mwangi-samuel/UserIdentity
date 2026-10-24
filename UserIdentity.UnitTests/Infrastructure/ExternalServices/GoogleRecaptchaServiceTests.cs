using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.Extensions.Options;

using PolyzenKit.Infrastructure.ExternalServices;

using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Infrastructure.ExternalServices;

using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.ExternalServices;

public class GoogleRecaptchaServiceTests
{
  private readonly IHttpClientHelper _httpClientHelper;
  private readonly IOptions<GoogleRecaptchaSettings> _settingsOptions;

  public GoogleRecaptchaServiceTests()
  {
    _httpClientHelper = A.Fake<IHttpClientHelper>();
    _settingsOptions = Options.Create(new GoogleRecaptchaSettings
    {
      Enabled = true,
      SiteKey = "site-key"
    });
  }

  [Fact]
  public async Task VerifyTokenAsync_WhenDisabled_ReturnsTrue()
  {
    // Arrange
    var service = new GoogleRecaptchaService(_httpClientHelper, Options.Create(new GoogleRecaptchaSettings { Enabled = false }));

    // Act
    var result = await service.VerifyTokenAsync(null, TestContext.Current.CancellationToken);

    // Assert
    Assert.True(result);
    A.CallTo(_httpClientHelper).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task VerifyTokenAsync_WhenTokenMissing_ReturnsFalse(string? token)
  {
    // Arrange
    var service = new GoogleRecaptchaService(_httpClientHelper, _settingsOptions);

    // Act
    var result = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(result);
    A.CallTo(_httpClientHelper).MustNotHaveHappened();
  }

  [Fact]
  public async Task VerifyTokenAsync_WhenResponseValidAndScoreHigh_ReturnsTrue()
  {
    var token = "token";
    var requestMessage = new HttpRequestMessage();
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.CreateHttpRequestMessage))
      .WithReturnType<HttpRequestMessage>()
      .Returns(requestMessage);

    var response = new GoogleRecaptchaResponseDto { Success = true, Score = 0.9f };
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(response));

    var service = new GoogleRecaptchaService(_httpClientHelper, _settingsOptions);

    var result = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);

    Assert.True(result);
    A.CallTo(() => _httpClientHelper.CreateHttpRequestMessage(HttpMethod.Post, "siteverify")).MustHaveHappenedOnceExactly();
    A.CallTo(() => _httpClientHelper.SendRequestAsync<GoogleRecaptchaResponseDto>("GoogleRecaptcha", requestMessage, HttpStatusCode.OK, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task VerifyTokenAsync_WhenResponseNullOrLowScore_ReturnsFalse()
  {
    var token = "token";
    var requestMessage = new HttpRequestMessage();
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.CreateHttpRequestMessage))
      .WithReturnType<HttpRequestMessage>()
      .Returns(requestMessage);

    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(null));

    var service = new GoogleRecaptchaService(_httpClientHelper, _settingsOptions);

    var resultNull = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
    Assert.False(resultNull);

    var lowScoreResponse = new GoogleRecaptchaResponseDto { Success = true, Score = 0.3f };
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task<GoogleRecaptchaResponseDto?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDto?>(lowScoreResponse));

    var resultLowScore = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
    Assert.False(resultLowScore);
  }
}
