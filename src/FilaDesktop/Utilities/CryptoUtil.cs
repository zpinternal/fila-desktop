using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FilaDesktop.Utilities;

public static class CryptoUtil
{
    public static (byte[] PublicId, byte[] DecryptionKey) DeriveStoreMaterial(string masterKey)
    {
        var masterBytes = Encoding.UTF8.GetBytes(masterKey);
        var publicId = SHA1.HashData(masterBytes);

        var workFactor = (publicId[0] << 8) | publicId[1];
        var buffer = masterBytes;
        for (var i = 0; i < workFactor; i++)
        {
            buffer = SHA3_512.HashData(buffer);
        }

        return (publicId, buffer.Take(16).ToArray());
    }

    public static string? DecryptHandshakeLine(string lineBase64, byte[] localPublicId, byte[] decryptKey)
    {
        var payload = Convert.FromBase64String(lineBase64.Trim());
        if (payload.Length < 37)
        {
            return null;
        }

        if (!payload.Take(20).SequenceEqual(localPublicId))
        {
            return null;
        }

        var iv = payload[20..36];
        var cipher = payload[36..];

        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = decryptKey;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain).Trim();
    }

    public static byte[] GenerateMobileEnvelope(string mobilePublicKeyPem, string dailyKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(mobilePublicKeyPem.AsSpan());

        var sessionKey = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);

        var encryptedSession = rsa.Encrypt(sessionKey, RSAEncryptionPadding.Pkcs1);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = sessionKey;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(dailyKey), 0, Encoding.UTF8.GetByteCount(dailyKey));

        return encryptedSession.Concat(iv).Concat(cipher).ToArray();
    }
}
