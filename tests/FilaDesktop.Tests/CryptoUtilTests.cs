using System;
using System.Security.Cryptography;
using System.Text;
using FilaDesktop.Utilities;
using Xunit;

namespace FilaDesktop.Tests;

public sealed class CryptoUtilTests
{
    [Fact]
    public void GenerateMobileEnvelope_HasExpectedMinimumLayout()
    {
        using var rsa = RSA.Create(2048);
        var pem = ExportPublicPem(rsa);

        var envelope = CryptoUtil.GenerateMobileEnvelope(pem, new string('A', 50));

        Assert.True(envelope.Length >= 272);
        Assert.Equal(0, envelope.Length % 16);
    }

    [Fact]
    public void DeriveStoreMaterial_ReturnsStableOutputForSameMasterKey()
    {
        var left = CryptoUtil.DeriveStoreMaterial("abc-master");
        var right = CryptoUtil.DeriveStoreMaterial("abc-master");

        Assert.Equal(Convert.ToHexString(left.PublicId), Convert.ToHexString(right.PublicId));
        Assert.Equal(Convert.ToHexString(left.DecryptionKey), Convert.ToHexString(right.DecryptionKey));
    }

    private static string ExportPublicPem(RSA rsa)
    {
        var key = rsa.ExportSubjectPublicKeyInfo();
        var b64 = Convert.ToBase64String(key, Base64FormattingOptions.InsertLineBreaks);
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        sb.AppendLine(b64);
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}
