namespace Bloxstrap.Utility
{
    // snitch.out: multi-instance client directories.
    //
    // Roblox's warm-start detector keys on install path (the per-path .mtx
    // mutex embeds it) and Byfron pins the exe name, so a second client can
    // never start from the same directory, renamed or not (verified live:
    // same-dir second launch dies in 0.16s with "Deeplinking NoReload!",
    // renamed exe passes the gate but Byfron blocks the image, same-name
    // copy in another directory runs fine side by side).
    //
    // So instance N>1 launches from a mirrored copy of the version folder:
    // <versionDir>-multi{slot}. Slot 0 is the primary directory itself.
    static class ClientMirror
    {
        private const string LOG_IDENT = "ClientMirror";
        private const int MaxMirrorSlots = 2;

        public static string GetMirrorDir(string primaryDir, int slot) =>
            slot <= 0 ? primaryDir : $"{primaryDir}-multi{slot}";

        // slot is picked from how many clients already run (0 = primary).
        // returns the exe path to launch (mirror, or primary as fallback).
        public static string EnsureMirrorExe(string primaryDir, string exeName, int slot)
        {
            string mirrorDir = GetMirrorDir(primaryDir, slot);

            if (slot <= 0 || slot > MaxMirrorSlots)
                return Path.Combine(primaryDir, exeName);

            try
            {
                string marker = Path.Combine(mirrorDir, "snitch-mirror.json");
                string want = new DirectoryInfo(primaryDir).Name;

                // another bootstrapper may be building the mirror right now
                // (marker is written last) - wait for it instead of racing
                for (int i = 0; i < 120 && Directory.Exists(mirrorDir) && !File.Exists(marker); i++)
                    Thread.Sleep(1000);

                bool fresh = File.Exists(marker) && File.ReadAllText(marker).Contains(want);

                if (!fresh)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Building mirror slot {slot} for {want}...");

                    if (!HaveDiskSpace(primaryDir))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Not enough free disk space for a client mirror, using primary (second client may refuse to start)");
                        return Path.Combine(primaryDir, exeName);
                    }

                    Directory.CreateDirectory(mirrorDir);
                    CopyDirOverwrite(primaryDir, mirrorDir);
                    File.WriteAllText(marker, JsonSerializer.Serialize(new
                    {
                        Version = want,
                        BuiltUtc = DateTime.UtcNow
                    }));

                    App.Logger.WriteLine(LOG_IDENT, $"Mirror slot {slot} ready");
                }

                string mirrorExe = Path.Combine(mirrorDir, exeName);

                if (File.Exists(mirrorExe))
                    return mirrorExe;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Mirror setup failed, using primary (second client may refuse to start)");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return Path.Combine(primaryDir, exeName);
        }

        private static bool HaveDiskSpace(string primaryDir)
        {
            try
            {
                long free = new DriveInfo(Path.GetPathRoot(primaryDir)!).AvailableFreeSpace;
                return free > 4L * 1024 * 1024 * 1024;
            }
            catch
            {
                return true;
            }
        }

        private static void CopyDirOverwrite(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(dest, Path.GetFileName(file));

                try
                {
                    File.Copy(file, target, true);
                }
                catch (Exception ex)
                {
                    // a client may be running from an older mirror build and
                    // locking its files - same-version bits are identical, so
                    // skipping locked files is correct
                    App.Logger.WriteLine(LOG_IDENT, $"Skipped locked file {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            foreach (string dir in Directory.GetDirectories(source))
                CopyDirOverwrite(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }
    }
}
