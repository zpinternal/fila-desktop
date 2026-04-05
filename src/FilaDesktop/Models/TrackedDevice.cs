using System;

namespace FilaDesktop.Models;

public sealed class TrackedDevice
{
    public required string DeviceName { get; init; }

    public required string SerialId { get; init; }

    public DeviceState State { get; set; }

    public DateTimeOffset? LastUpdatedUtc { get; set; }

    public bool InCooldown => LastUpdatedUtc.HasValue && DateTimeOffset.UtcNow - LastUpdatedUtc.Value < TimeSpan.FromMinutes(1);
}
