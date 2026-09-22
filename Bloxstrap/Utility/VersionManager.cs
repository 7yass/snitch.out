namespace Bloxstrap.Utility
{
    public class InstalledVersion
    {
        public string VersionGuid { get; set; } = "";
        public bool HasPlayer { get; set; }
        public bool HasStudio { get; set; }
        public DateTime Modified { get; set; }

        public string DisplayName => VersionGuid switch
        {
            "" => "Latest (no pin)",
            _ => VersionGuid
        };

        public override string ToString() => DisplayName;
    }

    public static class VersionManager
    {
        private const string LOG_IDENT = "VersionManager";

        public static bool IsInstalled(string versionGuid)
        {
            if (String.IsNullOrEmpty(versionGuid) || !versionGuid.StartsWith("version-"))
                return false;

            if (!Paths.Initialized || !Directory.Exists(Paths.Versions))
                return false;

            string dir = Path.Combine(Paths.Versions, versionGuid);

            if (!Directory.Exists(dir))
                return false;

            // a complete install has at least one client executable
            return File.Exists(Path.Combine(dir, App.RobloxPlayerAppName)) ||
                   File.Exists(Path.Combine(dir, App.RobloxStudioAppName));
        }

        public static List<InstalledVersion> GetInstalledVersions()
        {
            var list = new List<InstalledVersion>();

            if (!Paths.Initialized || !Directory.Exists(Paths.Versions))
                return list;

            foreach (string dir in Directory.GetDirectories(Paths.Versions))
            {
                string name = Path.GetFileName(dir);

                if (!name.StartsWith("version-"))
                    continue;

                try
                {
                    list.Add(new InstalledVersion
                    {
                        VersionGuid = name,
                        HasPlayer = File.Exists(Path.Combine(dir, App.RobloxPlayerAppName)),
                        HasStudio = File.Exists(Path.Combine(dir, App.RobloxStudioAppName)),
                        Modified = Directory.GetLastWriteTimeUtc(dir)
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            return list.OrderByDescending(x => x.Modified).ToList();
        }

        public static string? GetPinnedVersion()
        {
            string pinned = App.Settings.Prop.PinnedVersionGuid;

            if (String.IsNullOrEmpty(pinned))
                return null;

            return pinned;
        }

        public static bool IsPinnedAndInstalled()
        {
            string? pinned = GetPinnedVersion();

            if (pinned is null)
                return false;

            return IsInstalled(pinned);
        }
    }
}
