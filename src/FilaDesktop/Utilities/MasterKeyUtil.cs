using System;
using Microsoft.Win32;

namespace FilaDesktop.Utilities;

public static class MasterKeyUtil
{
    private const string PrimaryPath = @"SOFTWARE\\Microsoft\\Cryptography";
    private const string FallbackPath = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ShellIcons";

    public static string GetOrCreateMasterKey()
    {
        var primary = ReadRegistryValue(PrimaryPath, "MachineGuid");
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        var fallback = ReadRegistryValue(FallbackPath, "MachineGuid");
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        var generated = Guid.NewGuid().ToString();
        using var key = Registry.LocalMachine.CreateSubKey(FallbackPath, true);
        key?.SetValue("MachineGuid", generated, RegistryValueKind.String);
        return generated;
    }

    private static string? ReadRegistryValue(string path, string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(path, false);
        return key?.GetValue(name)?.ToString();
    }
}
