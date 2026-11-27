using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using PolyzenKit.Infrastructure.Security.AppSignatures;
using Xunit;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class TestBackend
{
    public const string Name = "otherservice";

    private static readonly Uri _serviceAddress = new("http://localhost");

    private readonly AsymmetricCipherKeyPair _keyPair = EdDsaHelper.GenerateEd25519KeyPair();

    public string PublicKeyPem => EdDsaHelper.ConvertToPem(_keyPair.Public);

    public async Task<HttpRequestMessage> CreateSignedRequestAsync(
        HttpMethod method,
        string path,
        object body,
        string appName,
        string backendName = Name,
        DateTimeOffset? signedAt = null
    )
    {
        var httpRequest = new HttpRequestMessage(method, new Uri(_serviceAddress, path))
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body))),
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
        httpRequest.AddXApiKey();

        await new AppRequestSigner((Ed25519PrivateKeyParameters)_keyPair.Private).SignAsync(
            httpRequest,
            appName,
            backendName,
            signedAt ?? DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken
        );

        return httpRequest;
    }
}
