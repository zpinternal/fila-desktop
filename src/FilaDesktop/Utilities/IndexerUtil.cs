using System;
using System.Collections.Generic;
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
                    if (parts.Length == 2)
                    {
                        var keyDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                        found[keyDate] = decrypted;
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
}
