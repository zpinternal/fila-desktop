using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FilaDesktop.Utilities;
using Xunit;

namespace FilaDesktop.Tests;

public sealed class IndexerUtilTests
{
    [Fact]
    public void Scan_UsesDerivedDayTokenDateAndPreservesDistinctEntries()
    {
        var profile = CreateTempProfile();
        try
        {
            var masterKey = "scan-master-key";
            var dropDir = Path.Combine(profile, "Desktop", "FILA");
            Directory.CreateDirectory(dropDir);

            var lineA = CreateTargetedLine(masterKey, "001-alpha-key");
            var lineB = CreateTargetedLine(masterKey, "200-beta-key");
            File.WriteAllLines(Path.Combine(dropDir, "SYNC-LATEST-KEY.FILA"), new[] { lineA, lineB });

            var sut = new IndexerUtil(profile);
            var result = sut.Scan(masterKey);

            var expectedDateA = ResolveExpectedDate(1, DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var expectedDateB = ResolveExpectedDate(200, DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            Assert.Equal(2, result.Count);
            Assert.Equal("001-alpha-key", result[expectedDateA]);
            Assert.Equal("200-beta-key", result[expectedDateB]);
        }
        finally
        {
            DeleteDir(profile);
        }
    }

    [Fact]
    public void Scan_SkipsLinesWithNonNumericDayPrefix()
    {
        var profile = CreateTempProfile();
        try
        {
            var masterKey = "scan-master-key";
            var dropDir = Path.Combine(profile, "Desktop", "FILA");
            Directory.CreateDirectory(dropDir);

            var invalid = CreateTargetedLine(masterKey, "bad-prefix-key");
            File.WriteAllText(Path.Combine(dropDir, "INVALID-LATEST-KEY.FILA"), invalid);

            var sut = new IndexerUtil(profile);
            var result = sut.Scan(masterKey);

            Assert.Empty(result);
        }
        finally
        {
            DeleteDir(profile);
        }
    }

    private static string CreateTargetedLine(string masterKey, string plainText)
    {
        var (publicId, decryptKey) = CryptoUtil.DeriveStoreMaterial(masterKey);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = decryptKey;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(publicId.Concat(iv).Concat(cipher).ToArray());
    }

    private static DateTime ResolveExpectedDate(int dayOfYear, DateTime utcNow)
    {
        var today = utcNow.Date;
        var candidateYears = new[] { today.Year - 1, today.Year, today.Year + 1 };
        return candidateYears
            .Where(year => dayOfYear <= (DateTime.IsLeapYear(year) ? 366 : 365))
            .Select(year => new DateTime(year, 1, 1).AddDays(dayOfYear - 1))
            .OrderBy(candidate => Math.Abs((candidate - today).TotalDays))
            .ThenByDescending(candidate => candidate)
            .First();
    }

    private static string CreateTempProfile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fila-profile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDir(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
