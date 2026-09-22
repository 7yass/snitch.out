using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Installer
{
    public class InstallViewModel : NotifyPropertyChangedViewModel
    {
        private readonly Bloxstrap.Installer installer = new();

        private readonly string _originalInstallLocation;

        public EventHandler<bool>? SetCanContinueEvent;

        public string InstallLocation 
        {
            get => installer.InstallLocation;
            set
            {
                if (!String.IsNullOrEmpty(ErrorMessage))
                {
                    SetCanContinueEvent?.Invoke(this, true);

                    installer.InstallLocationError = "";
                    OnPropertyChanged(nameof(ErrorMessage));
                }

                installer.InstallLocation = value;
                OnPropertyChanged(nameof(DataFoundMessageVisibility));
                OnPropertyChanged(nameof(PrereqDiskText));
            }
        }

        public Visibility DataFoundMessageVisibility => installer.ExistingDataPresent ? Visibility.Visible : Visibility.Collapsed;

        public string ErrorMessage => installer.InstallLocationError;

        public bool CreateDesktopShortcuts
        {
            get => installer.CreateDesktopShortcuts;
            set => installer.CreateDesktopShortcuts = value;
        }
        
        public bool CreateStartMenuShortcuts
        {
            get => installer.CreateStartMenuShortcuts;
            set => installer.CreateStartMenuShortcuts = value;
        }

        public bool ImportSettings
        {
            get => installer.ImportSettings;
            set => installer.ImportSettings = value;
        }

        public bool ImportSettingsEnabled
        {
            get => Directory.Exists(installer.BloxstrapInstallDirectory);
        }

        public bool ShowNotFound // im lazy
        {
            get => !Directory.Exists(installer.BloxstrapInstallDirectory);
        }

        // snitch.out: Froststrap-style prerequisite readout
        public string PrereqOsText => $"Windows {Environment.OSVersion.Version}";

        public string PrereqWebView2Text
        {
            get
            {
                string? version = GetWebView2Version();
                return version is null
                    ? "Not found - installed automatically with Roblox"
                    : $"Version {version}";
            }
        }

        public string PrereqDiskText
        {
            get
            {
                try
                {
                    string root = Path.GetPathRoot(Path.GetFullPath(InstallLocation))!;
                    var drive = new DriveInfo(root);
                    double freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
                    return $"{freeGb:0.0} GB free (3 GB recommended)";
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        private static string? GetWebView2Version()
        {
            const string subkey = @"SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

            foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                using var key = root.OpenSubKey(subkey);
                if (key?.GetValue("pv") is string version && !String.IsNullOrEmpty(version))
                    return version;
            }

            return null;
        }

        public ICommand BrowseInstallLocationCommand => new RelayCommand(BrowseInstallLocation);

        public ICommand ResetInstallLocationCommand => new RelayCommand(ResetInstallLocation);

        public ICommand OpenFolderCommand => new RelayCommand(OpenFolder);

        public InstallViewModel()
        {
            _originalInstallLocation = installer.InstallLocation;
        }

        public bool DoInstall()
        {
            if (!installer.CheckInstallLocation())
            {
                SetCanContinueEvent?.Invoke(this, false);

                OnPropertyChanged(nameof(ErrorMessage));
                return false;
            }

            installer.DoInstall();

            return true;
        }

        private void BrowseInstallLocation()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            InstallLocation = dialog.SelectedPath;
            OnPropertyChanged(nameof(InstallLocation));
        }

        private void ResetInstallLocation()
        {
            InstallLocation = _originalInstallLocation;
            OnPropertyChanged(nameof(InstallLocation));
        }

        private void OpenFolder() => Process.Start("explorer.exe", Paths.Base);
    }
}
