using Bloxstrap.Models.SettingTasks.Base;

namespace Bloxstrap.Models.SettingTasks
{
    // snitch.out: user-picked cursor PNG applied to both near + far cursors,
    // mirrors FontModPresetTask
    public class CursorFileModPresetTask : StringBaseTask
    {
        private static readonly string[] PackCursorResources = new[]
        {
            "Cursor.From2006.ArrowCursor.png",
            "Cursor.From2013.ArrowCursor.png"
        };

        private static bool MatchesPackCursor()
        {
            if (!File.Exists(Paths.CustomCursor))
                return false;

            byte[] fileHash;
            using (var fileStream = File.OpenRead(Paths.CustomCursor))
                fileHash = App.MD5Provider.ComputeHash(fileStream);

            foreach (string resource in PackCursorResources)
            {
                using var stream = Resource.GetStream(resource);
                if (App.MD5Provider.ComputeHash(stream).SequenceEqual(fileHash))
                    return true;
            }

            return false;
        }

        public CursorFileModPresetTask() : base("ModPreset", "CustomCursor")
        {
            // a cursor pack (2006/2013) writes to the same paths - only treat
            // the file as custom when it is not pack-provided
            if (File.Exists(Paths.CustomCursor) && !MatchesPackCursor())
                OriginalState = Paths.CustomCursor;
        }

        private static void CopyTo(string dest, string source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            Filesystem.AssertReadOnly(dest);
            File.Copy(source, dest, true);
        }

        private static void Delete(string dest)
        {
            if (!File.Exists(dest))
                return;

            Filesystem.AssertReadOnly(dest);
            File.Delete(dest);
        }

        public override void Execute()
        {
            if (!String.IsNullOrEmpty(NewState))
            {
                if (File.Exists(NewState))
                {
                    if (String.Compare(NewState, Paths.CustomCursor, StringComparison.InvariantCultureIgnoreCase) != 0)
                        CopyTo(Paths.CustomCursor, NewState);

                    CopyTo(Paths.CustomFarCursor, NewState);
                }
            }
            else
            {
                Delete(Paths.CustomCursor);
                Delete(Paths.CustomFarCursor);
            }

            OriginalState = NewState;
        }
    }
}
