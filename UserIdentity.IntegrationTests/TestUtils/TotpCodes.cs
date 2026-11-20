using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class TotpCodes
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int TimeStepSeconds = 30;

    public static string For(string base32Key, DateTimeOffset? at = null)
    {
        var timeStep = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / TimeStepSeconds;
        var counter = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA1, FromBase32(base32Key));
        hmac.AppendData(counter);
        var hash = hmac.GetHashAndReset();
        var offset = hash[^1] & 0xf;
        var binaryCode = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7fffffff;

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] FromBase32(string base32Key)
    {
        var characters = base32Key.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in characters)
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)(buffer >> bitsInBuffer));
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        return [.. bytes];
    }
}
