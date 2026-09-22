using Bloxstrap.Models.SettingTasks.Base;

namespace Bloxstrap.Models.SettingTasks
{
    // snitch.out: user-picked death sound (ouch.mp3), mirrors FontModPresetTask
    public class DeathSoundModPresetTask : StringBaseTask
    {
        public DeathSoundModPresetTask() : base("ModPreset", "DeathSound")
        {
            if (File.Exists(Paths.CustomDeathSound))
                OriginalState = Paths.CustomDeathSound;
        }

        public override void Execute()
        {
            if (!String.IsNullOrEmpty(NewState))
            {
                if (String.Compare(NewState, Paths.CustomDeathSound, StringComparison.InvariantCultureIgnoreCase) != 0 && File.Exists(NewState))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Paths.CustomDeathSound)!);

                    Filesystem.AssertReadOnly(Paths.CustomDeathSound);
                    File.Copy(NewState, Paths.CustomDeathSound, true);
                }
            }
            else if (File.Exists(Paths.CustomDeathSound))
            {
                Filesystem.AssertReadOnly(Paths.CustomDeathSound);
                File.Delete(Paths.CustomDeathSound);
            }

            OriginalState = NewState;
        }
    }
}
