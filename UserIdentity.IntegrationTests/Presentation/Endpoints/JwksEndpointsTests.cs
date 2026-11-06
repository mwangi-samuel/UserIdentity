using System.Net;
using Newtonsoft.Json.Linq;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Presentation.Endpoints;

public class JwksEndpointsTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private static readonly string _baseUri = "/api/v1/jwks/keys";

    [Fact]
    public async Task Get_KeySets_Returns_KeySets()
    {
        // Arrange & Act
        var response = await HttpClient.SendNoAuthRequestAsync(HttpMethod.Get, _baseUri);

        // Assert
        var responseString = await response.ValidateRequestResponseAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var jsonObject = SerDeHelper.Deserialize<JObject>(responseString);

        Assert.NotNull(jsonObject);

        var keySets = jsonObject?.ToObject<IDictionary<string, IList<Dictionary<string, string>>>>();

        Assert.NotNull(keySets);

        Assert.Equal(1, keySets?.Count);

        var keysetList = keySets?["keys"];
        Assert.NotNull(keysetList);
        Assert.Equal(1, keysetList?.Count);

        Assert.Equal(5, keysetList?[0].Count);

        Assert.Equal("EdDSA", keysetList?[0]["alg"]);
        Assert.Equal("OKP", keysetList?[0]["kty"]);
        Assert.Equal("Ed25519", keysetList?[0]["crv"]);
        Assert.NotNull(keysetList?[0]["x"]);
        Assert.NotNull(keysetList?[0]["kid"]);
    }

    [Fact]
    public async Task Get_KeySets_Without_Api_Key_Returns_KeySets()
    {
        // Arrange - the JWKS endpoint is listed in ApiKeySettings:ExcludedUris, so callers need no X-Api-Key
        using var request = new HttpRequestMessage(HttpMethod.Get, _baseUri);

        // Act
        var response = await HttpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
