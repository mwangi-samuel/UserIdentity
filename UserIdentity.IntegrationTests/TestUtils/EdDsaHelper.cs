using System.IO;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class EdDsaHelper
{
  public static AsymmetricCipherKeyPair GenerateEd25519KeyPair()
  {
    var keyPairGenerator = new Ed25519KeyPairGenerator();
    keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
    return keyPairGenerator.GenerateKeyPair();
  }

  public static string ConvertToPem(AsymmetricKeyParameter key, string? passphrase = null)
  {
    using var stringWriter = new StringWriter();
    var pemWriter = new PemWriter(stringWriter);
    if (passphrase != null)
    {
      var encryptor = new Pkcs8Generator(key, Pkcs8Generator.PbeWithShaAnd3KeyTripleDesCbc)
      {
        Password = passphrase.ToCharArray()
      };
      pemWriter.WriteObject(encryptor);
    }
    else
    {
      pemWriter.WriteObject(key);
    }

    pemWriter.Writer.Flush();
    return stringWriter.ToString();
  }
}
