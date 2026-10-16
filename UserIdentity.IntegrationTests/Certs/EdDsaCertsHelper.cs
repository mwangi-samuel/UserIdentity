using PolyzenKit.Common.Utilities;

using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Certs;

public class EdDsaCertsHelper(
   ITestOutputHelper testOutputHelper
  )
{
  private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

  [Fact]
  public void Generating_Certs_Pair_Generates_And_Prints_As_Expected()
  {
    var privateKeyPassPhrase = StringUtil.GenerateRandomString(32, true, true);

    var keyPair = EdDsaHelper.GenerateEd25519KeyPair();

    var privateKeyPem = EdDsaHelper.ConvertToPem(keyPair.Private, privateKeyPassPhrase);
    var publicKeyPem = EdDsaHelper.ConvertToPem(keyPair.Public);

    _testOutputHelper.WriteLine("Private Key Pem");
    _testOutputHelper.WriteLine(privateKeyPem);

    _testOutputHelper.WriteLine("Private Key Pem Passphrase");
    _testOutputHelper.WriteLine(privateKeyPassPhrase);

    _testOutputHelper.WriteLine("Private Key Pem");
    _testOutputHelper.WriteLine(publicKeyPem);
  }
}
