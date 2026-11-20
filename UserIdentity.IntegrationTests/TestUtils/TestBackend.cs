using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Signers;
using PolyzenKit.Common.Utilities;

namespace UserIdentity.IntegrationTests.TestUtils;

public sealed class TestBackend
{
    public const string Name = "otherservice";

    private readonly AsymmetricCipherKeyPair _keyPair = EdDsaHelper.GenerateEd25519KeyPair();

    public string PublicKeyPem => EdDsaHelper.ConvertToPem(_keyPair.Public);

    public HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        string path,
        object body,
        string appName,
        string backendName = Name,
        DateTimeOffset? signedAt = null
    )
    {
        var bodyBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body));
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        var httpRequest = new HttpRequestMessage(method, path) { Content = new ByteArrayContent(bodyBytes) };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
        httpRequest.AddXApiKey();
        httpRequest.Headers.Add(CustomHeaderNames.AppName, appName);
        httpRequest.Headers.Add(CustomHeaderNames.AppBackend, backendName);
        httpRequest.Headers.Add(CustomHeaderNames.Timestamp, timestamp);
        httpRequest.Headers.Add(
            CustomHeaderNames.Signature,
            Sign([.. Encoding.UTF8.GetBytes($"{timestamp}\n{method.Method}\n{path}\n"), .. bodyBytes])
        );

        return httpRequest;
    }

    private string Sign(byte[] signedBytes)
    {
        Ed25519Signer signer = new();
        signer.Init(true, _keyPair.Private);
        signer.BlockUpdate(signedBytes, 0, signedBytes.Length);

        return Convert.ToBase64String(signer.GenerateSignature());
    }
}
