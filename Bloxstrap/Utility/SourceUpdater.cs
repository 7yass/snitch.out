using System.IO.Compression;
using System.Text.Json;

namespace Bloxstrap.Utility
{
    // snitch.out: source-based auto-updater and installer.
    // instead of downloading pre-built release binaries, this pulls the
    // source straight from GitHub, compiles it with the local dotnet SDK,
    // and installs the built output over the existing app.
    //
    // flow:
    //   1. GET /repos/7yass/snitch.out/commits/main  -> latest commit SHA
    //   2. compare with LastCommitSha in Settings
    //   3. if newer: download main.zip, extract, dotnet publish, install
    //   4. restart the app
    static class SourceUpdater
    {
        private const string LOG_IDENT = "SourceUpdater";

        // github serves the latest main-branch source as a zip at this URL
        private const string SourceZipUrl =
            "https://github.com/7yass/snitch.out/archive/refs/heads/main.zip";

        public static async Task<bool> CheckAndUpdateAsync()
        {
            if (!App.Settings.Prop.CheckForUpdates)
                return false;

            if (App.LaunchSettings.QuietFlag.Active ||
                App.LaunchSettings.UninstallFlag.Active ||
                App.LaunchSettings.WatcherFlag.Active ||
                App.LaunchSettings.TestModeFlag.Active)
                return false;

            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
            {
                App.Logger.WriteLine(LOG_IDENT, "Another instance running, skipping update check");
                return false;
            }

            string? latestSha = await GetLatestCommitSha();
            if (String.IsNullOrEmpty(latestSha))
                return false;

            string currentSha = App.Settings.Prop.LastCommitSha ?? "";

            if (latestSha.Equals(currentSha, StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Already on commit {latestSha}");
                return false;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Update available: {currentSha} -> {latestSha}");

            string? outputDir = await DownloadAndBuildAsync();
            if (outputDir is null)
                return false;

            InstallBuiltApp(outputDir);

            // remember the commit
            App.Settings.Prop.LastCommitSha = latestSha;
            App.Settings.Save();

            return true;
        }

        // snitch.out: first-time install from source. downloads, builds, and
        // copies every file (including the exe) into installDir.
        public static async Task<bool> InstallFromSourceAsync(string installDir)
        {
            App.Logger.WriteLine(LOG_IDENT, "Installing snitch.out from source...");

            string? outputDir = await DownloadAndBuildAsync();
            if (outputDir is null)
                return false;

            Directory.CreateDirectory(installDir);

            foreach (string file in Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(outputDir.Length + 1);
                string target = Path.Combine(installDir, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
                App.Logger.WriteLine(LOG_IDENT, $"Installed {relative}");
            }

            return true;
        }

        private static async Task<string?> GetLatestCommitSha()
        {
            try
            {
                var commit = await Http.GetJson<GithubCommit>(
                    new Uri($"https://api.github.com/repos/{App.ProjectRepository}/commits/main"));

                return String.IsNullOrEmpty(commit?.Sha) ? null : commit.Sha;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        // downloads the source zipball, extracts it, and runs dotnet publish.
        // returns the publish output directory, or null on failure.
        private static async Task<string?> DownloadAndBuildAsync()
        {
            string tempBase = Path.Combine(Paths.Temp, "snitch-update");
            string sourceDir = Path.Combine(tempBase, "source");
            string outputDir = Path.Combine(tempBase, "publish");

            try
            {
                Directory.CreateDirectory(tempBase);

                // 1. download source zip
                App.Logger.WriteLine(LOG_IDENT, "Downloading source from GitHub...");
                string zipPath = Path.Combine(tempBase, "snitch.source.zip");

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                using var response = await App.HttpClient.GetAsync(
                    SourceZipUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var fileStream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write);
                await using var responseStream = await response.Content.ReadAsStreamAsync();
                await responseStream.CopyToAsync(fileStream);

                // 2. extract
                App.Logger.WriteLine(LOG_IDENT, "Extracting source...");
                if (Directory.Exists(sourceDir))
                    Directory.Delete(sourceDir, true);

                ZipFile.ExtractToDirectory(zipPath, sourceDir);

                // the zipball extracts into snitch.out-main/
                string projectRoot = Directory.GetDirectories(sourceDir)[0];

                // 3. build with dotnet publish
                App.Logger.WriteLine(LOG_IDENT, "Building from source...");
                Directory.CreateDirectory(outputDir);

                string csproj = Path.Combine(projectRoot, "Bloxstrap", "Bloxstrap.csproj");
                if (!File.Exists(csproj))
                {
                    App.Logger.WriteLine(LOG_IDENT, "Bloxstrap.csproj not found in source");
                    return null;
                }

                var buildInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"publish \"{csproj}\" -c Release -o \"{outputDir}\" --nologo",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var build = Process.Start(buildInfo)!;
                string buildOut = await build.StandardOutput.ReadToEndAsync();
                string buildErr = await build.StandardError.ReadToEndAsync();
                build.WaitForExit();

                if (build.ExitCode != 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Build failed (exit {build.ExitCode})");
                    App.Logger.WriteLine(LOG_IDENT, buildErr);
                    return null;
                }

                App.Logger.WriteLine(LOG_IDENT, "Build succeeded");
                return outputDir;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }

        private static void InstallBuiltApp(string outputDir)
        {
            const string LOG_IDENT = "SourceUpdater::InstallBuiltApp";

            // the running exe is locked, so we copy every file except the exe
            // over the install directory, then spawn a restart helper that
            // swaps the exe and relaunches after we exit.
            string installDir = Paths.Base;

            Directory.CreateDirectory(installDir);

            foreach (string file in Directory.GetFiles(outputDir, "*.*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(outputDir.Length + 1);

                // skip the exe - it's locked by the running process;
                // the restart helper will swap it
                if (relative.Equals($"{App.ProjectName}.exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                string target = Path.Combine(installDir, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
                App.Logger.WriteLine(LOG_IDENT, $"Installed {relative}");
            }

            // spawn a helper that replaces the running exe and relaunches
            string helperArgs = $"-updaterestart \"{Path.Combine(outputDir, $"{App.ProjectName}.exe")}\" \"{installDir}\"";
            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.Process,
                Arguments = helperArgs,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            App.Terminate();
        }
    }
}
