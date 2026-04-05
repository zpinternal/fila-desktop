using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using FilaDesktop.Models;
using FilaDesktop.Utilities;

namespace FilaDesktop.Services;

public sealed class DeviceTrackerService : IDisposable
{
    private readonly ConcurrentDictionary<string, TrackedDevice> _cache = new();
    private readonly Timer _fallbackTimer;
    private readonly ManagementEventWatcher? _insertWatcher;
    private readonly ManagementEventWatcher? _removeWatcher;

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
        var devices = DeviceUtil.ListDevices();
        var connectedSerials = devices
            .Select(d => d.SerialId)
            .Where(serial => !string.IsNullOrWhiteSpace(serial))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        PruneDisconnectedDevices(connectedSerials);

        foreach (var device in devices)
        {
            if (string.IsNullOrWhiteSpace(device.SerialId))
            {
                continue;
            }

            var state = ResolveState(device.SerialId);
            var existing = _cache.TryGetValue(device.SerialId, out var cached) ? cached : null;

            _cache[device.SerialId] = new TrackedDevice
            {
                DeviceName = device.DeviceName,
                SerialId = device.SerialId,
                State = existing?.State == DeviceState.Updated && state == DeviceState.Ready
                    ? DeviceState.Updated
                    : state,
                LastUpdatedUtc = existing?.LastUpdatedUtc
            };
        }

        DevicesChanged?.Invoke(this, _cache.Values.ToArray());
        return Task.CompletedTask;
    }

    private static DeviceState ResolveState(string serialId)
    {
        try
        {
            return DeviceUtil.FindFilaFolder(serialId) ? DeviceState.Ready : DeviceState.FilaNotFound;
        }
        catch
        {
            return DeviceState.FilaNotFound;
        }
    }

    public IReadOnlyCollection<TrackedDevice> Snapshot() => _cache.Values.ToArray();

    public void Upsert(TrackedDevice device)
    {
        _cache[device.SerialId] = device;
        DevicesChanged?.Invoke(this, _cache.Values.ToArray());
    }

    public void MarkUpdated(string serial)
    {
        if (_cache.TryGetValue(serial, out var existing))
        {
            existing.State = DeviceState.Updated;
            existing.LastUpdatedUtc = DateTimeOffset.UtcNow;
            DevicesChanged?.Invoke(this, _cache.Values.ToArray());
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
}
