using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FilaDesktop.Models;
using FilaDesktop.Services;
using FilaDesktop.Utilities;

namespace FilaDesktop.UI;

public sealed class MainForm : Form
{
    private readonly Label _vaultLabel = new() { AutoSize = true };
    private readonly Label _hostLabel = new() { AutoSize = true, Left = 320 };
    private readonly DataGridView _deviceGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false };
    private readonly Button _scanButton = new() { Text = "Scan Devices" };
    private readonly Button _updateButton = new() { Text = "Update Selected Device", Enabled = false };
    private readonly CheckBox _autoUpdateCheck = new() { Text = "Auto-Update" };
    private readonly RichTextBox _logBox = new() { Dock = DockStyle.Fill, ReadOnly = true };

    private readonly DeviceTrackerService _tracker = new();
    private readonly VaultUtil _vault;
    private readonly IndexerUtil _indexer;
    private readonly string _masterKey;

    public MainForm()
    {
        Text = "FILA Store Client";
        Width = 1000;
        Height = 700;

        _masterKey = MasterKeyUtil.GetOrCreateMasterKey();
        _vault = new VaultUtil(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FilaDesktop"));
        _indexer = new IndexerUtil(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        BuildUi();
        HookEvents();

        _tracker.Start();
        _ = Task.Run(LoadInitialStateAsync);
    }

    private void BuildUi()
    {
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 36 };
        topPanel.Controls.Add(_vaultLabel);
        topPanel.Controls.Add(_hostLabel);

        _deviceGrid.Columns.Add("DeviceName", "Device Name");
        _deviceGrid.Columns.Add("SerialId", "Serial ID");
        _deviceGrid.Columns.Add("State", "State");

        var middlePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        middlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        middlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var scanPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
        scanPanel.Controls.Add(_scanButton);

        var deviceContainer = new Panel { Dock = DockStyle.Fill };
        deviceContainer.Controls.Add(_deviceGrid);
        deviceContainer.Controls.Add(scanPanel);

        middlePanel.Controls.Add(deviceContainer, 0, 0);
        middlePanel.Controls.Add(_logBox, 0, 1);

        var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
        bottomPanel.Controls.Add(_updateButton);
        bottomPanel.Controls.Add(_autoUpdateCheck);

        Controls.Add(middlePanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
    }

    private void HookEvents()
    {
        _tracker.DevicesChanged += (_, devices) => BeginInvoke(() => RefreshGrid(devices));
        _scanButton.Click += async (_, _) => await ScanAsync();
        _updateButton.Click += async (_, _) => await UpdateSelectedDeviceAsync();
        _deviceGrid.SelectionChanged += (_, _) => UpdateUpdateButtonState();
    }

    private async Task LoadInitialStateAsync()
    {
        await ScanAsync();
        await _tracker.RefreshAsync();

        var vaultCount = _vault.Load().Count;
        BeginInvoke(() =>
        {
            _vaultLabel.Text = $"Found {vaultCount} Keys in Vault";
            _hostLabel.Text = $"Host Key ID: {Convert.ToHexString(CryptoUtil.DeriveStoreMaterial(_masterKey).PublicId)}";
        });
    }

    private async Task ScanAsync()
    {
        await Task.Run(() =>
        {
            var items = _indexer.Scan(_masterKey);
            foreach (var item in items)
            {
                _vault.Save(item.Key, item.Value);
            }

            Log($"Scan complete. Imported {items.Count} key(s).");
            BeginInvoke(() => _vaultLabel.Text = $"Found {_vault.Load().Count} Keys in Vault");
        });
    }

    private Task UpdateSelectedDeviceAsync()
    {
        if (_deviceGrid.SelectedRows.Count == 0)
        {
            return Task.CompletedTask;
        }

        var serial = _deviceGrid.SelectedRows[0].Cells["SerialId"].Value?.ToString();
        if (string.IsNullOrWhiteSpace(serial))
        {
            return Task.CompletedTask;
        }

        _tracker.MarkUpdated(serial);
        Log($"Updated {serial} successfully.");
        return Task.CompletedTask;
    }

    private void RefreshGrid(System.Collections.Generic.IReadOnlyCollection<TrackedDevice> devices)
    {
        _deviceGrid.Rows.Clear();
        foreach (var d in devices.OrderBy(x => x.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            _deviceGrid.Rows.Add(d.DeviceName, d.SerialId, ToDisplayState(d));
        }

        UpdateUpdateButtonState();
    }

    private static string ToDisplayState(TrackedDevice device)
    {
        return device.State switch
        {
            DeviceState.Ready => "READY",
            DeviceState.Outdated => "OUTDATED",
            DeviceState.FilaNotFound => "FILA NOT FOUND",
            DeviceState.Updated => "UPDATED",
            _ => "UNKNOWN"
        };
    }

    private void UpdateUpdateButtonState()
    {
        _updateButton.Enabled = _deviceGrid.SelectedRows.Count > 0 && string.Equals(
            _deviceGrid.SelectedRows[0].Cells["State"].Value?.ToString(),
            "READY",
            StringComparison.OrdinalIgnoreCase);
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        BeginInvoke(() =>
        {
            _logBox.AppendText(line);
            _logBox.ScrollToCaret();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tracker.Dispose();
        }

        base.Dispose(disposing);
    }
}
