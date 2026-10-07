namespace Bloxstrap.UI.ViewModels.Settings
{
    // snitch.out: one row in the mod file manager
    public class ModFileRow
    {
        public string FullPath { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string Target { get; set; } = "Both";
        public string SizeText { get; set; } = "";
    }
}
