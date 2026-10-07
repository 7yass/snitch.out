using System.Windows;

namespace Bloxstrap.Utility
{
    // snitch.out: update checks with a prompt, usable from the menu and
    // settings windows (the bootstrapper only checks on game launch).
    // Supports the stable channel (tagged releases) and the nightly
    // channel (every main commit, matched by commit hash).
    static class AppUpdater
    {
        private const string LOG_IDENT = "AppUpdater";

        private static bool _checkedThisSession = false;

        public static async Task<bool> CheckAndPromptAsync()
        {
            if (_checkedThisSession)
                return false;

            _checkedThisSession = true;

            if (!App.Settings.Prop.CheckForUpdates || App.LaunchSettings.UpgradeFlag.Active)
                return false;

            if (App.LaunchSettings.QuietFlag.Active || App.LaunchSettings.UninstallFlag.Active ||
                App.LaunchSettings.WatcherFlag.Active || App.LaunchSettings.TestModeFlag.Active)
                return false;

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Another instance running, skipping update check");
                return false;
            }

            try
            {
                if (App.Settings.Prop.UseNightlyBuilds)
                    return await CheckNightlyAsync();

                return await CheckStableAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static async Task<bool> CheckStableAsync()
        {
            var release = await App.GetLatestRelease();

            if (release is null)
                return false;

            if (Utilities.CompareVersions(App.Version, release.TagName) != VersionComparison.LessThan)
            {
                App.Logger.WriteLine(LOG_IDENT, "No stable updates found");
                return false;
            }

            var answer = Frontend.ShowMessageBox(
                $"snitch.out {release.TagName} is available (you have v{App.Version}). Download and install it now?",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (answer != MessageBoxResult.Yes)
                return false;

            return await DownloadAndApplyAsync(release);
        }

        public static async Task<bool> CheckNightlyAsync()
        {
            _checkedThisSession = true;

            // local builds have no commit to compare against
            if (!App.IsActionBuild || String.IsNullOrEmpty(App.BuildMetadata.CommitHash))
            {
                App.Logger.WriteLine(LOG_IDENT, "Not an action build, skipping nightly check");
                return false;
            }

            GithubRelease? release;

            try
            {
                release = await Http.GetJson<GithubRelease>(
                    new Uri($"https://api.github.com/repos/{App.ProjectRepository}/releases/tags/nightly"));
            }
            catch
            {
                App.Logger.WriteLine(LOG_IDENT, "No nightly release found");
                return false;
            }

            if (release is null || String.IsNullOrEmpty(release.TargetCommitish))
                return false;

            if (release.TargetCommitish.StartsWith(App.BuildMetadata.CommitHash, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, "Nightly is up to date");
                return false;
            }

            string shortSha = release.TargetCommitish.Length > 7 ? release.TargetCommitish[..7] : release.TargetCommitish;

            var answer = Frontend.ShowMessageBox(
                $"A newer nightly build is available ({shortSha}, you have {App.BuildMetadata.CommitHash}). Download and install it now?",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo
            );

            if (answer != MessageBoxResult.Yes)
                return false;

            return await DownloadAndApplyAsync(release);
        }

        public static async Task<bool> DownloadAndApplyAsync(GithubRelease release)
        {
            const string IDENT = "AppUpdater::DownloadAndApplyAsync";

            var asset = release.Assets!
                .FirstOrDefault(x => x.Name.Equals($"{App.ProjectName}.exe", StringComparison.OrdinalIgnoreCase))
                ?? release.Assets![0];

            string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);

            Directory.CreateDirectory(Paths.TempUpdates);

            try
            {
                // always re-download: nightly builds reuse the same filename
                if (File.Exists(downloadLocation))
                    File.Delete(downloadLocation);

                App.Logger.WriteLine(IDENT, $"Downloading {asset.Name}...");

                var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);

                await using var fileStream = new FileStream(downloadLocation, FileMode.CreateNew, FileAccess.Write);
                await response.Content.CopyToAsync(fileStream);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(IDENT, "Download failed");
                App.Logger.WriteException(IDENT, ex);

                Frontend.ShowMessageBox(
                    $"Failed to download the update.\n\nGet it manually from {App.ProjectDownloadLink}",
                    MessageBoxImage.Warning
                );

                return false;
            }

            App.Logger.WriteLine(IDENT, "Starting updater...");

            ProcessStartInfo startInfo = new() { FileName = downloadLocation };
            startInfo.ArgumentList.Add("-upgrade");

            foreach (string arg in App.LaunchSettings.Args)
                startInfo.ArgumentList.Add(arg);

            App.Settings.Save();

            new InterProcessLock("AutoUpdater");

            Process.Start(startInfo);
            App.Terminate();

            return true;
        }
    }
}
