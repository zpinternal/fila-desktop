using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MediaDevices;

namespace FilaDesktop.Utilities;

public static class DeviceUtil
{
    /// <summary>
    /// Immutable identity details for a detected mobile device.
    /// </summary>
    /// <param name="DeviceId">Unique MTP device identifier provided by the OS.</param>
    /// <param name="SerialId">Stable device serial used by the app for tracking.</param>
    /// <param name="DeviceName">Human-friendly device name.</param>
    public readonly record struct DeviceIdentity(string DeviceId, string SerialId, string DeviceName);

    /// <summary>
    /// Enumerates currently attached media devices and returns immutable identity values.
    /// </summary>
    /// <remarks>
    /// Ownership contract: this method does not return <see cref="MediaDevice"/> instances.
    /// Callers should pass <see cref="DeviceIdentity.SerialId"/> into other utility methods,
    /// which create and dispose native handles internally.
    /// </remarks>
    public static IReadOnlyList<DeviceIdentity> ListDevices()
    {
        MediaDevice.RefreshDeviceList();
        return MediaDevice.GetDevices()
            .Select(device => new DeviceIdentity(device.DeviceId, device.SerialNumber, device.FriendlyName))
            .ToArray();
    }

    /// <summary>
    /// Checks whether the target device contains the root-level <c>\\FILA</c> folder.
    /// </summary>
    /// <param name="serialId">Device serial identifier from <see cref="ListDevices"/>.</param>
    /// <returns><see langword="true"/> when the FILA folder exists; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Ownership contract: the method resolves the device by serial and fully owns connection,
    /// disconnection, and disposal of the <see cref="MediaDevice"/> instance.
    /// </remarks>
    public static bool FindFilaFolder(string serialId)
    {
        return UseConnectedDevice(serialId, device =>
        {
            foreach (var root in device.GetDirectories("\\"))
            {
                if (string.Equals(Path.GetFileName(root.TrimEnd('\\')), "FILA", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        });
    }

    /// <summary>
    /// Reads the contents of <c>\\FILA\\MOBILE.KEY</c> from the target device.
    /// </summary>
    /// <param name="serialId">Device serial identifier from <see cref="ListDevices"/>.</param>
    /// <returns>Text content of the mobile key file.</returns>
    /// <remarks>
    /// Ownership contract: the method resolves the device by serial and fully owns connection,
    /// disconnection, and disposal of the <see cref="MediaDevice"/> instance.
    /// </remarks>
    public static string PullMobileKey(string serialId)
    {
        return UseConnectedDevice(serialId, device =>
        {
            using var stream = device.OpenRead("\\FILA\\MOBILE.KEY");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    /// <summary>
    /// Writes key data to <c>\\FILA\\KEYS.FILA</c> on the target device.
    /// </summary>
    /// <param name="serialId">Device serial identifier from <see cref="ListDevices"/>.</param>
    /// <param name="data">Serialized key payload bytes.</param>
    /// <remarks>
    /// Ownership contract: the method resolves the device by serial and fully owns connection,
    /// disconnection, and disposal of the <see cref="MediaDevice"/> instance.
    /// </remarks>
    public static void PushKey(string serialId, byte[] data)
    {
        UseConnectedDevice(serialId, device =>
        {
            using var ms = new MemoryStream(data);
            device.UploadFile(ms, "\\FILA\\KEYS.FILA", overwrite: true);
            return true;
        });
    }

    private static T UseConnectedDevice<T>(string serialId, Func<MediaDevice, T> operation)
    {
        if (string.IsNullOrWhiteSpace(serialId))
        {
            throw new ArgumentException("Device serial ID is required.", nameof(serialId));
        }

        MediaDevice.RefreshDeviceList();
        using var device = MediaDevice.GetDevices()
            .FirstOrDefault(d => string.Equals(d.SerialNumber, serialId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unable to find connected device with serial '{serialId}'.");

        var connectedHere = !device.IsConnected;
        if (connectedHere)
        {
            device.Connect();
        }

        try
        {
            return operation(device);
        }
        finally
        {
            if (connectedHere && device.IsConnected)
            {
                device.Disconnect();
            }
        }
    }
}
