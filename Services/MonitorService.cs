using System.Runtime.InteropServices;
using ScreenChanger.Models;

namespace ScreenChanger.Services;

public static class DisplayConfigService
{
    #region Win32 structs

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public int OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DISPLAYCONFIG_RATIONAL RefreshRate;
        public uint ScanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)] public bool TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO SourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO TargetInfo;
        public uint Flags;
    }

    // 64-byte opaque struct: 4+4+8 header + 48-byte union
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint InfoType;
        public uint Id;
        public LUID AdapterId;
        public long U0, U1, U2, U3, U4, U5;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint Type;
        public uint Size;
        public LUID AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint Flags;
        public int OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    #endregion

    #region P/Invoke

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPaths, out uint numModes);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DISPLAYCONFIG_PATH_INFO[] pathArray,
        uint numModeInfoArrayElements,
        [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        uint flags);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    private const uint QDC_ALL_PATHS              = 0x00000001;
    private const uint DISPLAYCONFIG_PATH_ACTIVE  = 0x00000001;
    private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    private const uint SDC_APPLY                  = 0x00000080;
    private const uint SDC_SAVE_TO_DATABASE       = 0x00000200;
    private const uint SDC_ALLOW_CHANGES          = 0x00000400;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    #endregion

    public static int ActiveIndex { get; private set; } = -1;

    public static MonitorInfo[] GetConnectedMonitors()
    {
        var (paths, _) = QueryAllPaths();

        var seen = new HashSet<(uint, int, uint)>();
        var raw = new List<(uint SourceId, string Name, uint AdapterLow, int AdapterHigh, uint TargetId)>();

        foreach (var path in paths)
        {
            if (!path.TargetInfo.TargetAvailable) continue;
            var key = (path.TargetInfo.AdapterId.LowPart, path.TargetInfo.AdapterId.HighPart, path.TargetInfo.Id);
            if (!seen.Add(key)) continue;

            string name = GetFriendlyName(path.TargetInfo.AdapterId, path.TargetInfo.Id)
                       ?? $"Монитор {raw.Count + 1}";
            raw.Add((path.SourceInfo.Id, name,
                path.TargetInfo.AdapterId.LowPart, path.TargetInfo.AdapterId.HighPart,
                path.TargetInfo.Id));
        }

        // Sort by GDI source ID — matches the "Display 1 / Display 2" order in Windows Settings
        raw.Sort((a, b) => a.SourceId.CompareTo(b.SourceId));

        var monitors = raw.Select((r, i) =>
            new MonitorInfo(i, r.Name, r.AdapterLow, r.AdapterHigh, r.TargetId)).ToList();

        var activeTargets = paths
            .Where(p => (p.Flags & DISPLAYCONFIG_PATH_ACTIVE) != 0 && p.TargetInfo.TargetAvailable)
            .Select(p => (p.TargetInfo.AdapterId.LowPart, p.TargetInfo.AdapterId.HighPart, p.TargetInfo.Id))
            .Distinct()
            .ToList();

        ActiveIndex = activeTargets.Count == 1
            ? monitors.FindIndex(m =>
                m.AdapterLow == activeTargets[0].LowPart &&
                m.AdapterHigh == activeTargets[0].HighPart &&
                m.TargetId == activeTargets[0].Id)
            : -1;

        return [.. monitors];
    }

    public static void ActivateOnly(MonitorInfo monitor)
    {
        var (paths, modes) = QueryAllPaths();

        bool found = false;
        for (int i = 0; i < paths.Length; i++)
        {
            bool isTarget = paths[i].TargetInfo.AdapterId.LowPart == monitor.AdapterLow
                         && paths[i].TargetInfo.AdapterId.HighPart == monitor.AdapterHigh
                         && paths[i].TargetInfo.Id == monitor.TargetId;

            if (isTarget && !found)
            {
                paths[i].Flags |= DISPLAYCONFIG_PATH_ACTIVE;
                // Let Windows choose modes via SDC_ALLOW_CHANGES
                paths[i].SourceInfo.ModeInfoIdx = 0xFFFFFFFF;
                paths[i].TargetInfo.ModeInfoIdx = 0xFFFFFFFF;
                found = true;
            }
            else
            {
                paths[i].Flags &= ~DISPLAYCONFIG_PATH_ACTIVE;
            }
        }

        if (!found) return;

        uint flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_SAVE_TO_DATABASE;
        if (SetDisplayConfig((uint)paths.Length, paths, (uint)modes.Length, modes, flags) == 0)
            ActiveIndex = monitor.Index;
    }

    private static (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) QueryAllPaths()
    {
        GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out uint numPaths, out uint numModes);
        var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
        var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
        QueryDisplayConfig(QDC_ALL_PATHS, ref numPaths, paths, ref numModes, modes, IntPtr.Zero);
        return (paths[..(int)numPaths], modes[..(int)numModes]);
    }

    private static string? GetFriendlyName(LUID adapterId, uint targetId)
    {
        var req = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                Type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                AdapterId = adapterId,
                Id = targetId
            }
        };
        return DisplayConfigGetDeviceInfo(ref req) == 0 && !string.IsNullOrWhiteSpace(req.MonitorFriendlyDeviceName)
            ? req.MonitorFriendlyDeviceName
            : null;
    }
}
