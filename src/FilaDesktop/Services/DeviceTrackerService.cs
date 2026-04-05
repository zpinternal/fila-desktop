using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FilaDesktop.Models;
using FilaDesktop.Utilities;
using MediaDevices;

namespace FilaDesktop.Services;

public sealed class DeviceTrackerService : IDisposable
{
    private readonly ConcurrentDictionary<string, TrackedDevice> _cache = new();
    private readonly object _eventGate = new();
    private readonly Timer _fallbackTimer;
    private readonly ManagementEventWatcher? _insertWatcher;
    private readonly ManagementEventWatcher? _removeWatcher;
    private bool _suppressEvents;

    public event EventHandler<IReadOnlyCollection<TrackedDevice>>? DevicesChanged;

    public DeviceTrackerService()
    {
        _fallbackTimer = new Timer(_ => _ = RefreshAsync(), null, Timeout.Infinite, Timeout.Infinite);

        try
        {
            _insertWatcher = new ManagementEventWatcher("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            _removeWatcher = new ManagementEventWatcher("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");
            _insertWatcher.EventArrived += (_, _) => _ = RefreshAsync();
            _removeWatcher.EventArrived += (_, _) => _ = RefreshAsync();
        }
        catch
        {
            _insertWatcher = null;
            _removeWatcher = null;
        }
    }

    public void Start()
    {
        if (_insertWatcher is not null && _removeWatcher is not null)
        {
            _insertWatcher.Start();
            _removeWatcher.Start();
        }
        else
        {
            _fallbackTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
    }

    public Task RefreshAsync()
    {
        var connectedSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _suppressEvents = true;
        try
        {
            foreach (var device in DeviceUtil.ListDevices())
            {
                using var _ = device;

                var serial = GetStableSerialId(device);
                connectedSerials.Add(serial);

                var existing = _cache.TryGetValue(serial, out var tracked) ? tracked : null;
                if (existing is not null && existing.State == DeviceState.Updated && existing.InCooldown)
                {
                    Upsert(new TrackedDevice
                    {
                        DeviceName = ResolveDeviceName(device, serial),
                        SerialId = serial,
                        State = DeviceState.Updated,
                        LastUpdatedUtc = existing.LastUpdatedUtc
                    });
                    continue;
                }

                var nextState = ResolveState(device);
                Upsert(new TrackedDevice
                {
                    DeviceName = ResolveDeviceName(device, serial),
                    SerialId = serial,
                    State = nextState,
                    LastUpdatedUtc = nextState == DeviceState.Updated ? existing?.LastUpdatedUtc : null
                });
            }
        }
        finally
        {
            _suppressEvents = false;
        }

        PruneDisconnectedDevices(connectedSerials);
        EmitDevicesChanged();
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<TrackedDevice> Snapshot() => _cache.Values.ToArray();

    public void Upsert(TrackedDevice device)
    {
        _cache[device.SerialId] = device;
        if (!_suppressEvents)
        {
            EmitDevicesChanged();
        }
    }

    public void MarkUpdated(string serial)
    {
        if (_cache.TryGetValue(serial, out var existing))
        {
            existing.State = DeviceState.Updated;
            existing.LastUpdatedUtc = DateTimeOffset.UtcNow;
            if (!_suppressEvents)
            {
                EmitDevicesChanged();
            }
        }
    }

    private void PruneDisconnectedDevices(IReadOnlyCollection<string> connectedSerials)
    {
        var connected = connectedSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var serial in _cache.Keys)
        {
            if (!connected.Contains(serial))
            {
                _cache.TryRemove(serial, out _);
            }
        }
    }

    public void Dispose()
    {
        _fallbackTimer.Dispose();
        _insertWatcher?.Dispose();
        _removeWatcher?.Dispose();
    }

    private DeviceState ResolveState(MediaDevice device)
    {
        try
        {
            device.Connect();

            var hasFilaFolder = HasFilaFolder(device);
            if (!hasFilaFolder)
            {
                return DeviceState.FilaNotFound;
            }

            var hasMobileKey = HasMobileKey(device);
            return hasMobileKey ? DeviceState.Ready : DeviceState.Outdated;
        }
        catch
        {
            return DeviceState.FilaNotFound;
        }
    }

    private static bool HasFilaFolder(MediaDevice device)
    {
        foreach (var root in device.GetDirectories("\\"))
        {
            if (string.Equals(System.IO.Path.GetFileName(root.TrimEnd('\\')), "FILA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMobileKey(MediaDevice device)
    {
        var fileExistsMethod = device.GetType().GetMethod("FileExists", new[] { typeof(string) });
        if (fileExistsMethod is not null && fileExistsMethod.ReturnType == typeof(bool))
        {
            try
            {
                return (bool)fileExistsMethod.Invoke(device, new object[] { "\\FILA\\MOBILE.KEY" })!;
            }
            catch
            {
                // Fall through to OpenRead probe.
            }
        }

        try
        {
            using var stream = device.OpenRead("\\FILA\\MOBILE.KEY");
            return stream is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveDeviceName(MediaDevice device, string fallback)
    {
        var friendly = ReadStringProperty(device, "FriendlyName");
        return string.IsNullOrWhiteSpace(friendly) ? fallback : friendly;
    }

    private static string GetStableSerialId(MediaDevice device)
    {
        var primary = ReadStringProperty(device, "SerialNumber");
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        var secondary = ReadStringProperty(device, "DeviceId");
        if (!string.IsNullOrWhiteSpace(secondary))
        {
            return secondary;
        }

        var tertiary = ReadStringProperty(device, "FriendlyName");
        if (!string.IsNullOrWhiteSpace(tertiary))
        {
            return tertiary;
        }

        var manufacturer = ReadStringProperty(device, "Manufacturer");
        var model = ReadStringProperty(device, "Model");
        var compound = $"{manufacturer}|{model}";
        return string.IsNullOrWhiteSpace(compound.Replace("|", string.Empty, StringComparison.Ordinal))
            ? "UNKNOWN-DEVICE"
            : compound;
    }

    private static string? ReadStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(instance)?.ToString()?.Trim();
    }

    private void EmitDevicesChanged()
    {
        lock (_eventGate)
        {
            DevicesChanged?.Invoke(this, _cache.Values.ToArray());
        }
    }
}
