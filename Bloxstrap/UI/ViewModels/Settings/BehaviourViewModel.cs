namespace Bloxstrap.UI.ViewModels.Settings
{
    public class BehaviourViewModel : NotifyPropertyChangedViewModel
    {

        public BehaviourViewModel()
        {
            App.Cookies.StateChanged += (object? _, CookieState state) => CookieLoadingFailed = state != CookieState.Success && state != CookieState.Unknown;
        }

        public bool IsRobloxInstallationMissing => String.IsNullOrEmpty(App.RobloxState.Prop.Player.VersionGuid) && String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public bool CookieAccess
        {
            get => App.Settings.Prop.AllowCookieAccess;
            set
            {
                App.Settings.Prop.AllowCookieAccess = value;
                if (value)
                    Task.Run(App.Cookies.LoadCookies);

                OnPropertyChanged(nameof(CookieAccess));
            }
        }

        // guh
        private bool _cookieLoadingFailed;
        public bool CookieLoadingFailed
        {
            get => _cookieLoadingFailed;
            set
            {
                _cookieLoadingFailed = value;
                OnPropertyChanged(nameof(CookieLoadingFailed));
            }
        }

        public bool EnableBetterMatchmaking
        {
            get => App.Settings.Prop.EnableBetterMatchmaking;
            set => App.Settings.Prop.EnableBetterMatchmaking = value;
        }

        public bool EnableBetterMatchmakingRandomization
        {
            get => App.Settings.Prop.EnableBetterMatchmakingRandomization;
            set => App.Settings.Prop.EnableBetterMatchmakingRandomization = value;
        }

        public bool ConfirmLaunches
        {
            get => App.Settings.Prop.ConfirmLaunches;
            set => App.Settings.Prop.ConfirmLaunches = value;
        }

        public bool ForceRobloxLanguage
        {
            get => App.Settings.Prop.ForceRobloxLanguage;
            set => App.Settings.Prop.ForceRobloxLanguage = value;
        }

        public bool BackgroundUpdates
        {
            get => App.Settings.Prop.BackgroundUpdatesEnabled;
            set => App.Settings.Prop.BackgroundUpdatesEnabled = value;
        }

        public CleanerOptions SelectedCleanUpMode
        {
            get => App.Settings.Prop.CleanerOptions;
            set => App.Settings.Prop.CleanerOptions = value;
        }

        public IEnumerable<CleanerOptions> CleanerOptions { get; } = CleanerOptionsEx.Selections;

        public CleanerOptions CleanerOption
        {
            get => App.Settings.Prop.CleanerOptions;
            set
            {
                App.Settings.Prop.CleanerOptions = value;
            }
        }

        private List<string> CleanerItems = App.Settings.Prop.CleanerDirectories;

        public bool CleanerLogs
        {
            get => CleanerItems.Contains("RobloxLogs");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxLogs");
                else
                    CleanerItems.Remove("RobloxLogs"); // should we try catch it?
            }
        }

        public bool CleanerCache
        {
            get => CleanerItems.Contains("RobloxCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxCache");
                else
                    CleanerItems.Remove("RobloxCache");
            }
        }

        public bool CleanerStudioCache
        {
            get => CleanerItems.Contains("RobloxStudioCache");
            set
            {
                if (value)
                    CleanerItems.Add("RobloxStudioCache");
                else
                    CleanerItems.Remove("RobloxStudioCache");
            }
        }

        public bool CleanerFishstrap
        {
            get => CleanerItems.Contains("SnitchLogs");
            set
            {
                if (value)
                    CleanerItems.Add("SnitchLogs");
                else
                    CleanerItems.Remove("SnitchLogs");
            }
        }

        // snitch.out: version snapshot keeper
        public bool KeepOldVersions
        {
            get => App.Settings.Prop.KeepOldVersions;
            set
            {
                App.Settings.Prop.KeepOldVersions = value;
                OnPropertyChanged(nameof(KeepOldVersions));
            }
        }

        public List<string> InstalledVersionOptions
        {
            get
            {
                var list = new List<string> { "" };
                try
                {
                    list.AddRange(Bloxstrap.Utility.VersionManager.GetInstalledVersions().Select(x => x.VersionGuid));
                }
                catch { }
                return list;
            }
        }

        public string PinnedVersionGuid
        {
            get => App.Settings.Prop.PinnedVersionGuid;
            set
            {
                App.Settings.Prop.PinnedVersionGuid = value ?? "";
                OnPropertyChanged(nameof(PinnedVersionGuid));
                OnPropertyChanged(nameof(PinStatusText));
            }
        }

        public string PinStatusText
        {
            get
            {
                string pinned = App.Settings.Prop.PinnedVersionGuid;
                if (String.IsNullOrEmpty(pinned))
                    return "Using latest Roblox version.";
                if (Bloxstrap.Utility.VersionManager.IsInstalled(pinned))
                    return $"Pinned to {pinned} (cached). Updates blocked. Live servers may reject old clients - best for Studio / local testing.";
                return $"Pinned to {pinned} but it is not cached - will fall back to latest.";
            }
        }

        public void RefreshVersions()
        {
            OnPropertyChanged(nameof(InstalledVersionOptions));
            OnPropertyChanged(nameof(PinStatusText));
        }
    }
}
