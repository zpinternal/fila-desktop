using System;
using System.Security;
using Microsoft.Win32;

namespace FilaDesktop.Utilities;

public static class MasterKeyUtil
{
    private const string PrimaryPath = @"SOFTWARE\\Microsoft\\Cryptography";
    private const string LegacyMachineFallbackPath = @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ShellIcons";
    private const string UserFallbackPath = @"SOFTWARE\\FilaDesktop";
    private const string MasterKeyName = "MachineGuid";

    public static string GetOrCreateMasterKey()
    {
        var primary = ReadRegistryValue(Registry.LocalMachine, PrimaryPath, MasterKeyName);
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        // Keep compatibility with any pre-existing machine-level fallback values.
        var legacyMachineFallback = ReadRegistryValue(Registry.LocalMachine, LegacyMachineFallbackPath, MasterKeyName);
        if (!string.IsNullOrWhiteSpace(legacyMachineFallback))
        {
            return legacyMachineFallback;
        }

        var userFallback = ReadRegistryValue(Registry.CurrentUser, UserFallbackPath, MasterKeyName);
        if (!string.IsNullOrWhiteSpace(userFallback))
        {
            return userFallback;
        }

        var generated = Guid.NewGuid().ToString();
        WriteRegistryValue(Registry.CurrentUser, UserFallbackPath, MasterKeyName, generated);
        return generated;
    }

    private static string? ReadRegistryValue(RegistryKey root, string path, string name)
    {
        try
        {
            using var key = root.OpenSubKey(path, false);
            return key?.GetValue(name)?.ToString();
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteRegistryValue(RegistryKey root, string path, string name, string value)
    {
        try
        {
            using var key = root.CreateSubKey(path, true);
            key?.SetValue(name, value, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            // If write fails, continue with in-memory generated key for this run.
        }
    }
}
