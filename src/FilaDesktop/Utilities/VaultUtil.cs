using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace FilaDesktop.Utilities;

public sealed class VaultUtil
{
    private const string StaticLocalKeySeed = "TextPOLOTo07252613";

    private readonly string _vaultPath;
    private readonly byte[] _vaultKey;

    public VaultUtil(string appDataFolder)
    {
        Directory.CreateDirectory(appDataFolder);
        _vaultPath = Path.Combine(appDataFolder, "vault.enc");
        _vaultKey = SHA256.HashData(Encoding.UTF8.GetBytes(StaticLocalKeySeed)).Take(16).ToArray();
    }

    public IReadOnlyDictionary<string, string> Load()
    {
        return LoadFromFile(_vaultPath) ?? LoadFromFile(_vaultPath + ".bak") ?? new Dictionary<string, string>();
    }

    public void Save(string date, string key)
    {
        var data = Load().ToDictionary(kv => kv.Key, kv => kv.Value);
        data[date] = key;

        var payload = JsonConvert.SerializeObject(data, Formatting.Indented);
        var encrypted = Encrypt(payload);

        var temp = _vaultPath + ".tmp";
        var bak = _vaultPath + ".bak";
        File.WriteAllBytes(temp, encrypted);

        if (File.Exists(_vaultPath))
        {
            File.Copy(_vaultPath, bak, true);
        }

        File.Move(temp, _vaultPath, true);
    }

    public string? GetLatest()
    {
        var data = Load();
        return data.Count == 0 ? null : data.OrderBy(kv => kv.Key, StringComparer.Ordinal).Last().Value;
    }

    private IReadOnlyDictionary<string, string>? LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var raw = File.ReadAllBytes(path);
            if (raw.Length <= 16)
            {
                return null;
            }

            var iv = raw[..16];
            var cipher = raw[16..];
            using var aes = Aes.Create();
            aes.KeySize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _vaultKey;
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            var json = Encoding.UTF8.GetString(plain);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private byte[] Encrypt(string payload)
    {
        var plain = Encoding.UTF8.GetBytes(payload);
        using var aes = Aes.Create();
        aes.KeySize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _vaultKey;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return aes.IV.Concat(cipher).ToArray();
    }
}
