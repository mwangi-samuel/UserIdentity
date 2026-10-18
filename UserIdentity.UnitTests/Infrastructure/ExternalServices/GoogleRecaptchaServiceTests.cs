using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.Extensions.Options;

using PolyzenKit.Infrastructure.ExternalServices;

using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Domain.Users;
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
      SiteKey = "site-key"
    });
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

    var response = new GoogleRecaptchaResponseDTO { Success = true, Score = 0.9f };
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task<GoogleRecaptchaResponseDTO?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDTO?>(response));

    var service = new GoogleRecaptchaService(_httpClientHelper, _settingsOptions);

    var result = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);

    Assert.True(result);
    A.CallTo(() => _httpClientHelper.CreateHttpRequestMessage(HttpMethod.Post, "siteverify")).MustHaveHappenedOnceExactly();
    A.CallTo(() => _httpClientHelper.SendRequestAsync<GoogleRecaptchaResponseDTO>("GoogleRecaptcha", requestMessage, HttpStatusCode.OK, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
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
      .WithReturnType<Task<GoogleRecaptchaResponseDTO?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDTO?>(null));

    var service = new GoogleRecaptchaService(_httpClientHelper, _settingsOptions);

    var resultNull = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
    Assert.False(resultNull);

    var lowScoreResponse = new GoogleRecaptchaResponseDTO { Success = true, Score = 0.3f };
    A.CallTo(_httpClientHelper)
      .Where(call => call.Method.Name == nameof(IHttpClientHelper.SendRequestAsync))
      .WithReturnType<Task<GoogleRecaptchaResponseDTO?>>()
      .Returns(Task.FromResult<GoogleRecaptchaResponseDTO?>(lowScoreResponse));

    var resultLowScore = await service.VerifyTokenAsync(token, TestContext.Current.CancellationToken);
    Assert.False(resultLowScore);
  }
}
