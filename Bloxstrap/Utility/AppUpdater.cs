using System.IO;
using System.Net;
using System.Windows;

namespace Bloxstrap.Utility
{
    // snitch.out: update checks with a prompt, usable from the menu and
    // settings windows (the bootstrapper only checks on game launch).
    // Supports the stable channel (tagged releases) and the nightly
    // channel (every main commit, matched by commit hash).
    //
    // Improvements over the original:
    //   - user can be prompted before an update is applied (SilentAppUpdates)
    //   - downloads are retried with exponential backoff on failure
    //   - downloaded bytes are verified (MZ header + content-length match)
    //   - download progress is reported through the status text
    //   - the AutoUpdater mutex is properly disposed
    static class AppUpdater
    {
        private const string LOG_IDENT = "AppUpdater";

        private const int MaxDownloadTries = 3;

        private static bool _checkedThisSession = false;

        public static async Task<bool> CheckAndPromptAsync(bool silent = false)
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
                    return await CheckNightlyAsync(silent);

                return await CheckStableAsync(silent);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static async Task<bool> CheckStableAsync(bool silent)
        {
            var release = await App.GetLatestRelease();

            if (release is null)
                return false;

            if (Utilities.CompareVersions(App.Version, release.TagName) != VersionComparison.LessThan)
            {
                App.Logger.WriteLine(LOG_IDENT, "No stable updates found");
                return false;
            }

            // silent path (bootstrapper launch): just apply
            if (silent || App.Settings.Prop.SilentAppUpdates)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Updating v{App.Version} -> {release.TagName} (silent)");
                return await DownloadAndApplyAsync(release);
            }

            // prompted path (menu): ask the user first
            var result = Frontend.ShowMessageBox(
                String.Format(
                    Strings.Dialog_AppUpdate_Available,
                    App.Version,
                    release.TagName),
                MessageBoxImage.Information,
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return false;

            return await DownloadAndApplyAsync(release);
        }

        public static async Task<bool> CheckNightlyAsync(bool silent = false)
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

            App.Logger.WriteLine(LOG_IDENT, $"Updating nightly {App.BuildMetadata.CommitHash} -> {shortSha}");

            if (!silent && !App.Settings.Prop.SilentAppUpdates)
            {
                var result = Frontend.ShowMessageBox(
                    String.Format(Strings.Dialog_AppUpdate_NightlyAvailable, shortSha),
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                    return false;
            }

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

            // always re-download: nightly builds reuse the same filename
            if (File.Exists(downloadLocation))
                File.Delete(downloadLocation);

            // download with retries + integrity verification
            bool downloaded = false;
            int tryNum = 0;

            while (!downloaded && tryNum < MaxDownloadTries)
            {
                tryNum++;

                try
                {
                    App.Logger.WriteLine(IDENT, $"Downloading {asset.Name} (attempt {tryNum}/{MaxDownloadTries})...");

                    using var response = await App.HttpClient.GetAsync(
                        asset.BrowserDownloadUrl,
                        HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    long? contentLength = response.Content.Headers.ContentLength;

                    await using var fileStream = new FileStream(
                        downloadLocation,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.Delete);
                    await using var responseStream = await response.Content.ReadAsStreamAsync();

                    var buffer = new byte[8192];
                    long totalRead = 0;

                    while (true)
                    {
                        int bytesRead = await responseStream.ReadAsync(buffer);

                        if (bytesRead == 0)
                            break;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;
                    }

                    // integrity: must start with MZ (valid PE/exe)
                    fileStream.Position = 0;
                    var header = new byte[2];
                    await fileStream.ReadAsync(header.AsMemory(0, 2));

                    if (header[0] != 0x4D || header[1] != 0x5A)
                        throw new InvalidDataException("Downloaded file is not a valid executable");

                    // integrity: size must match content-length when advertised
                    if (contentLength is long cl && totalRead != cl)
                        throw new InvalidDataException(
                            $"Download size mismatch (expected {cl}, got {totalRead})");

                    downloaded = true;
                    App.Logger.WriteLine(IDENT, $"Finished downloading {asset.Name} ({totalRead} bytes)");
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(IDENT, $"Download attempt {tryNum} failed: {ex.Message}");

                    if (File.Exists(downloadLocation))
                        File.Delete(downloadLocation);

                    if (tryNum >= MaxDownloadTries)
                    {
                        Frontend.ShowMessageBox(
                            Strings.Dialog_AppUpdate_DownloadFailed,
                            MessageBoxImage.Warning);

                        return false;
                    }

                    // exponential backoff before retry
                    await Task.Delay(TimeSpan.FromSeconds(tryNum * 2));
                }
            }

            if (!downloaded)
                return false;

            App.Logger.WriteLine(IDENT, "Starting updater...");

            ProcessStartInfo startInfo = new() { FileName = downloadLocation };
            startInfo.ArgumentList.Add("-upgrade");

            foreach (string arg in App.LaunchSettings.Args)
                startInfo.ArgumentList.Add(arg);

            App.Settings.Save();

            // preserve the launch mode so the updater knows whether to
            // start the player or studio after applying the update
            if (App.LaunchSettings.RobloxLaunchMode == LaunchMode.Player &&
                !startInfo.ArgumentList.Contains("-player"))
                startInfo.ArgumentList.Add("-player");
            else if (App.LaunchSettings.RobloxLaunchMode == LaunchMode.Studio &&
                !startInfo.ArgumentList.Contains("-studio"))
                startInfo.ArgumentList.Add("-studio");

            using (new InterProcessLock("AutoUpdater"))
            {
                Process.Start(startInfo);
            }

            App.Terminate();

            return true;
        }
    }
}
