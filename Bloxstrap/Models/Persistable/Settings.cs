using DiscordRPC;
using System.Collections.ObjectModel;

namespace Bloxstrap.Models.Persistable
{
    public class Settings
    {
        // uh
        public bool AllowCookieAccess { get; set; } = false;

        // bloxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.CustomDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public RobloxIcon RobloxIcon { get; set; } = RobloxIcon.IconDefault;
        public string RobloxTitle { get; set; } = "Roblox";
        public string RobloxIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Dark;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        // PLEASE DONT FORGET TO TURN THIS OFF !!!!
        public bool UseAcrylicBackground { get; set; } = false;
        public byte AcrylicBackgroundOpacity { get; set; } = 165;
        public bool ForceLocalData { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        // snitch.out: last GitHub commit SHA the source-based updater built
        public string? LastCommitSha { get; set; }
        public bool ConfirmLaunches { get; set; } = true;
        public bool EnableRobloxBackgroundApp { get; set; } = false;
        public RobloxTheme RobloxTheme { get; set; } = RobloxTheme.Default;
        public string Locale { get; set; } = "nil";
        public bool ForceRobloxLanguage { get; set; } = false;
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = false;
        public bool StaticDirectory { get; set; } = false;
        public string Channel { get; set; } = RobloxInterfaces.Deployment.DefaultChannel;
        public string RobloxDomain { get; set; } = RobloxInterfaces.Deployment.DefaultRobloxDomain;
        public ChannelChangeMode ChannelChangeMode { get; set; } = ChannelChangeMode.Automatic;
        public string? SelectedCustomTheme { get; set; } = Utility.ThemeSeeder.BuiltInThemeName;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        // snitch.out: multi-instance support
        public bool AllowMultiInstance { get; set; } = false;
        // snitch.out: update channel (commit builds instead of tagged releases)
        public bool UseNightlyBuilds { get; set; } = false;
        // snitch.out: apply app updates silently without prompting the user first
        public bool SilentAppUpdates { get; set; } = false;
        // snitch.out: version snapshot keeper for testing older builds
        public string PinnedVersionGuid { get; set; } = "";
        public bool KeepOldVersions { get; set; } = true;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public bool EnableBetterMatchmaking { get; set; } = false;
        public bool EnableBetterMatchmakingRandomization { get; set; } = false;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // snitch.out: executor / external tool friendly mode
        // when enabled, DLLs dropped into the Modifications folder (dxgi.dll etc.)
        // are treated as injection targets and guaranteed to be copied into the
        // Roblox version folder on launch so externals and executors can attach.
        public bool ExternalInjectionEnabled { get; set; } = false;

        // snitch.out: injector DLLs to always keep in the version folder
        // (dll hijacking targets like dxgi.dll, version.dll, etc.)
        public List<string> InjectedDlls { get; set; } = new List<string>
        {
            "dxgi.dll",
            "d3d9.dll",
            "d3d11.dll",
            "opengl32.dll",
            "version.dll"
        };

        // integration configuration
        public CleanerOptions CleanerOptions { get; set; } = CleanerOptions.TwoWeeks;
        // how do i automate this? -Naveandice
        public List<string> CleanerDirectories { get; set; } = new List<string> {
            "RobloxCache",
            "RobloxStudioCache",
            "RobloxLogs",
            "SnitchLogs"
        };
        public bool EnableWindowManipulation { get; set; } = false;
        public bool FakeBorderlessFullscreen { get; set; } = false;
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public DiscordRPCStatusDisplay RichPresenceStatusDisplayType { get; set; } = DiscordRPCStatusDisplay.Name;
        // snitch.out: RPC extras
        public string RichPresenceDetailsPrefix { get; set; } = "";
        public bool ShowPlaytimeOnPresence { get; set; } = false;
        public bool UseStudioPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;
    }
}
