using System;
using System.IO;
using FilaDesktop.Utilities;
using Xunit;

namespace FilaDesktop.Tests;

public sealed class VaultUtilTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fila-vault-{Guid.NewGuid():N}");
        try
        {
            var vault = new VaultUtil(dir);
            vault.Save("2026-01-01", "001-example-key");

            var loaded = vault.Load();
            Assert.True(loaded.ContainsKey("2026-01-01"));
            Assert.Equal("001-example-key", loaded["2026-01-01"]);
            Assert.Equal("001-example-key", vault.GetLatest());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
