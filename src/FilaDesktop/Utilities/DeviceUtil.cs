using System;
using System.Collections.Generic;
using System.IO;
using MediaDevices;

namespace FilaDesktop.Utilities;

public static class DeviceUtil
{
    public static IReadOnlyList<MediaDevice> ListDevices()
    {
        MediaDevice.RefreshDeviceList();
        return MediaDevice.GetDevices();
    }

    public static bool FindFilaFolder(MediaDevice device)
    {
        device.Connect();
        foreach (var root in device.GetDirectories("\\"))
        {
            if (string.Equals(Path.GetFileName(root.TrimEnd('\\')), "FILA", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string PullMobileKey(MediaDevice device)
    {
        device.Connect();
        var path = "\\FILA\\MOBILE.KEY";
        using var stream = device.OpenRead(path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void PushKey(MediaDevice device, byte[] data)
    {
        device.Connect();
        using var ms = new MemoryStream(data);
        device.UploadFile(ms, "\\FILA\\KEYS.FILA", overwrite: true);
    }
}
