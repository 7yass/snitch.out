using Bloxstrap.RobloxInterfaces;
using Bloxstrap.Utility;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Bloxstrap.Models.Manifest;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ChannelViewModel : NotifyPropertyChangedViewModel
    {
        public bool IsRobloxInstallationMissing => String.IsNullOrEmpty(App.RobloxState.Prop.Player.VersionGuid) && String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public ChannelViewModel()
        {
            Task.Run(() => LoadChannelDeployInfo(App.Settings.Prop.Channel));
        }

        private bool ValidateDomain(string domain)
        {
            const string domainPattern = @"^([a-zA-Z0-9.-]+)\.([a-zA-Z0-9]+)$";

            return Regex.IsMatch(domain, domainPattern);
        }

        private async Task LoadChannelDeployInfo(string channel)
        {
            ShowLoadingError = false;
            OnPropertyChanged(nameof(ShowLoadingError));

            ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Fetching;
            OnPropertyChanged(nameof(ChannelInfoLoadingText));

            ChannelDeployInfo = null;
            OnPropertyChanged(nameof(ChannelDeployInfo));

            try
            {
                bool isPrivate = await Deployment.IsChannelPrivate(channel);
                if (App.Cookies.Loaded && isPrivate && String.IsNullOrEmpty(Deployment.ChannelToken))
                {
                    UserChannel? userChannel = await Deployment.GetUserChannel("WindowsPlayer");

                    if (userChannel?.Token is not null)
                        Deployment.ChannelToken = userChannel.Token;
                }

                ClientVersion info = await Deployment.GetInfo(channel, true, true);

                ShowChannelWarning = info.IsBehindDefaultChannel;
                OnPropertyChanged(nameof(ShowChannelWarning));

                ChannelDeployInfo = new DeployInfo
                {
                    Version = info.Version,
                    VersionGuid = isPrivate ? "version-private" : info.VersionGuid,
                    Timestamp = info.Timestamp?.ToLocalTime().ToString() ?? "?"
                };

                App.State.Prop.IgnoreOutdatedChannel = true;

                OnPropertyChanged(nameof(ChannelDeployInfo));
            }
            catch (InvalidChannelException ex)
            {
                ShowLoadingError = true;
                OnPropertyChanged(nameof(ShowLoadingError));

                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                    ChannelInfoLoadingText = Strings.Menu_Channel_Switcher_Unauthorized;
                else
                    ChannelInfoLoadingText = $"An http error has occured ({ex.StatusCode})";

                OnPropertyChanged(nameof(ChannelInfoLoadingText));
            }
        }

        public bool ShowLoadingError { get; set; } = false;
        public bool ShowChannelWarning { get; set; } = false;

        public DeployInfo? ChannelDeployInfo { get; private set; } = null;
        public string ChannelInfoLoadingText { get; private set; } = null!;

        // snitch.out: channel presets. "live" is a real channel now, not an alias for production.
        public IReadOnlyDictionary<string, string> ChannelPresets => new Dictionary<string, string>
        {
            { Strings.Menu_Channel_Preset_Production, Deployment.DefaultChannel },
            { Strings.Menu_Channel_Preset_Live, Deployment.LiveChannel },
            { Strings.Menu_Channel_Preset_Custom, "__custom__" }
        };

        public string SelectedChannelPreset
        {
            get
            {
                string current = App.Settings.Prop.Channel;
                var match = ChannelPresets.FirstOrDefault(x => x.Value.Equals(current, StringComparison.OrdinalIgnoreCase));
                return match.Key ?? Strings.Menu_Channel_Preset_Custom;
            }
            set
            {
                if (ChannelPresets.TryGetValue(value, out string? channel))
                {
                    if (channel == "__custom__")
                        return; // leave the custom textbox in control

                    App.Settings.Prop.Channel = channel;
                    ViewChannel = channel;
                    Task.Run(() => LoadChannelDeployInfo(channel));
                }
            }
        }

        public string ViewChannel
        {
            get => App.Settings.Prop.Channel;
            set
            {
                value = value.Trim();
                Task.Run(() => LoadChannelDeployInfo(value));

                // snitch.out: "live" is a real channel now. don't alias it to production.
                App.Settings.Prop.Channel = value;
                OnPropertyChanged(nameof(ViewChannel));
                OnPropertyChanged(nameof(SelectedChannelPreset));
            }
        }

        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        public bool StaticDirectory
        {
            get => App.Settings.Prop.StaticDirectory;
            set => App.Settings.Prop.StaticDirectory = value;
        }

        public string RobloxDomain
        {
            get => App.Settings.Prop.RobloxDomain;
            set
            {
                if (value.Equals("libstanpreg.so", StringComparison.OrdinalIgnoreCase))
                    Frontend.ShowMessageBox("libstanpreg.so is real.", MessageBoxImage.Hand);

                if (ValidateDomain(value))
                    App.Settings.Prop.RobloxDomain = value;
                else
                    Frontend.ShowMessageBox(Strings.Menu_Channel_RobloxDomain_InvalidDomain, MessageBoxImage.Warning, MessageBoxButton.OK);
            }
        }

        public bool ExternalInjectionEnabled
        {
            get => App.Settings.Prop.ExternalInjectionEnabled;
            set => App.Settings.Prop.ExternalInjectionEnabled = value;
        }

        public IReadOnlyDictionary<string, ChannelChangeMode> ChannelChangeModes => new Dictionary<string, ChannelChangeMode>
        {
            { Strings.Menu_Channel_ChangeAction_Automatic, ChannelChangeMode.Automatic },
            { Strings.Menu_Channel_ChangeAction_Prompt, ChannelChangeMode.Prompt },
            { Strings.Menu_Channel_ChangeAction_Ignore, ChannelChangeMode.Ignore },
        };

        public string SelectedChannelChangeMode
        {
            get => ChannelChangeModes.FirstOrDefault(x => x.Value == App.Settings.Prop.ChannelChangeMode).Key;
            set => App.Settings.Prop.ChannelChangeMode = ChannelChangeModes[value];
        }

        // snitch.out: version snapshots. go back to any cached Roblox build.
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
                    list.AddRange(VersionManager.GetInstalledVersions().Select(x => x.VersionGuid));
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
                    return Strings.Menu_Channel_PinStatus_Latest;
                if (VersionManager.IsInstalled(pinned))
                    return String.Format(Strings.Menu_Channel_PinStatus_Pinned, pinned);
                return String.Format(Strings.Menu_Channel_PinStatus_Missing, pinned);
            }
        }

        public void RefreshVersions()
        {
            OnPropertyChanged(nameof(InstalledVersionOptions));
            OnPropertyChanged(nameof(PinStatusText));
        }

        public string DownloadVersionGuid
        {
            get => _downloadVersionGuid;
            set
            {
                _downloadVersionGuid = value;
                OnPropertyChanged(nameof(DownloadVersionGuid));
            }
        }

        private string _downloadVersionGuid = "";

        public bool IsDownloadingVersion
        {
            get => _isDownloadingVersion;
            set
            {
                _isDownloadingVersion = value;
                OnPropertyChanged(nameof(IsDownloadingVersion));
                OnPropertyChanged(nameof(DownloadButtonText));
            }
        }

        private bool _isDownloadingVersion;

        public string DownloadButtonText => IsDownloadingVersion ? "Downloading..." : "Download & cache";

        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                _downloadStatus = value;
                OnPropertyChanged(nameof(DownloadStatus));
            }
        }

        private string _downloadStatus = "";

        public ICommand DownloadVersionCommand => new RelayCommand(DownloadVersion);

        private async void DownloadVersion()
        {
            string guid = DownloadVersionGuid.Trim();
            if (String.IsNullOrWhiteSpace(guid))
            {
                DownloadStatus = "Enter a version hash first.";
                return;
            }

            if (VersionManager.IsInstalled(guid))
            {
                DownloadStatus = "Already cached.";
                RefreshVersions();
                return;
            }

            IsDownloadingVersion = true;
            DownloadStatus = "Fetching manifest...";

            try
            {
                if (App.Bootstrapper is null)
                {
                    DownloadStatus = "Bootstrapper not available - launch snitch.out normally first.";
                    return;
                }

                bool ok = await App.Bootstrapper.DownloadCachedVersion(guid);

                if (ok)
                {
                    DownloadStatus = "Cached. Pin it in the dropdown above to launch.";
                    RefreshVersions();
                }
                else
                {
                    DownloadStatus = "Download failed - check the hash or try again.";
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = $"Error: {ex.Message}";
                App.Logger.WriteLine("ChannelViewModel", $"DownloadCachedVersion failed for {guid}");
                App.Logger.WriteException("ChannelViewModel", ex);
            }
            finally
            {
                IsDownloadingVersion = false;
            }
        }

        public ICommand OpenModificationsFolderCommand => new RelayCommand(OpenModificationsFolder);

        private void OpenModificationsFolder()
        {
            if (Directory.Exists(Paths.Modifications))
                Process.Start("explorer.exe", Paths.Modifications);
        }

        // snitch.out: injector DLL status for the deployment page.
        public List<InjectedDllEntry> InjectedDllStatus
        {
            get
            {
                var list = new List<InjectedDllEntry>();
                foreach (string dll in App.Settings.Prop.InjectedDlls)
                {
                    string path = Path.Combine(Paths.Modifications, dll);
                    list.Add(new InjectedDllEntry
                    {
                        Name = dll,
                        Status = File.Exists(path) ? Strings.Menu_Deployment_Injector_Ready : Strings.Menu_Deployment_Injector_Missing
                    });
                }
                return list;
            }
        }

        public class InjectedDllEntry
        {
            public string Name { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public bool ForceRobloxReinstallation
        {
            get => App.State.Prop.ForceReinstall || IsRobloxInstallationMissing;
            set => App.State.Prop.ForceReinstall = value;
        }
    }
}
