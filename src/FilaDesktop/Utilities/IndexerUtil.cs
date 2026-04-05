using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FilaDesktop.Utilities;

public sealed class IndexerUtil
{
    private readonly string _scanPath;

    public IndexerUtil(string userProfile)
    {
        _scanPath = Path.Combine(userProfile, "Desktop", "FILA");
    }

    public IReadOnlyDictionary<string, string> Scan(string masterKey)
    {
        var found = new Dictionary<string, string>();
        if (!Directory.Exists(_scanPath))
        {
            return found;
        }

        var (publicId, decryptKey) = CryptoUtil.DeriveStoreMaterial(masterKey);
        foreach (var file in Directory.EnumerateFiles(_scanPath, "*LATEST-KEY.FILA"))
        {
            foreach (var line in File.ReadLines(file).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                try
                {
                    var decrypted = CryptoUtil.DecryptHandshakeLine(line, publicId, decryptKey);
                    if (string.IsNullOrWhiteSpace(decrypted))
                    {
                        continue;
                    }

                    var parts = decrypted.Split('-', 2, StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && TryResolveKeyDate(parts[0], DateTime.UtcNow, out var keyDate))
                    {
                        found[keyDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = decrypted;
                    }
                }
                catch
                {
                    // ignore bad lines
                }
            }
        }

        return found;
    }

    private static bool TryResolveKeyDate(string dayToken, DateTime utcNow, out DateTime keyDate)
    {
        keyDate = default;
        if (!int.TryParse(dayToken, NumberStyles.None, CultureInfo.InvariantCulture, out var dayOfYear))
        {
            return false;
        }

        if (dayOfYear is < 1 or > 366)
        {
            return false;
        }

        var today = utcNow.Date;
        var candidateYears = new[] { today.Year - 1, today.Year, today.Year + 1 };
        var candidates = candidateYears
            .Where(year => dayOfYear <= (DateTime.IsLeapYear(year) ? 366 : 365))
            .Select(year => new DateTime(year, 1, 1).AddDays(dayOfYear - 1))
            .ToArray();

        if (candidates.Length == 0)
        {
            return false;
        }

        keyDate = candidates
            .OrderBy(candidate => Math.Abs((candidate - today).TotalDays))
            .ThenByDescending(candidate => candidate)
            .First();
        return true;
    }
}
