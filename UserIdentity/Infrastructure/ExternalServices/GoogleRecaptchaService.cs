using System.Net;

using Microsoft.Extensions.Options;

using PolyzenKit.Infrastructure.ExternalServices;

using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Interfaces;

namespace UserIdentity.Infrastructure.ExternalServices;

public class GoogleRecaptchaService(
    IHttpClientHelper httpClientHelper,
    IOptions<GoogleRecaptchaSettings> googleRecaptchaSettingsOptions
    ) : IGoogleRecaptchaService
{

  private readonly IHttpClientHelper _httpClientHelper = httpClientHelper;
  private readonly GoogleRecaptchaSettings _googleRecaptchaSettings = googleRecaptchaSettingsOptions.Value;

  private const string targetServiceName = "GoogleRecaptcha";
  public async Task<bool> VerifyTokenAsync(string? token, CancellationToken cancellationToken = default)
  {
    if (!_googleRecaptchaSettings.Enabled)
      return true;

    if (string.IsNullOrEmpty(token))
      return false;

    var requestParams = new Dictionary<string, string>
        {
            { "secret", _googleRecaptchaSettings.SiteKey!},
            { "response", token }
        };

    var requestMessage = _httpClientHelper.CreateHttpRequestMessage(HttpMethod.Post, "siteverify")
        .WithRequestParams(requestParams);

    var response = await _httpClientHelper.SendRequestAsync<GoogleRecaptchaResponseDto>(targetServiceName, requestMessage, HttpStatusCode.OK, cancellationToken);

    return response != null && response.Success && response.Score >= 0.5;
  }
}
