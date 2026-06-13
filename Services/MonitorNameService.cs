using System.Management;

namespace ScreenChanger.Services;

public static class MonitorNameService
{
    public static string[] GetMonitorNames()
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorID");
            var monitors = searcher.Get()
                .Cast<ManagementObject>()
                .OrderBy(o => (string)o["InstanceName"])
                .ToList();

            foreach (var obj in monitors)
            {
                var nameArray = obj["UserFriendlyName"] as ushort[];
                if (nameArray != null)
                {
                    var name = new string(nameArray.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
                obj.Dispose();
            }
        }
        catch { }

        return [.. names];
    }
}
