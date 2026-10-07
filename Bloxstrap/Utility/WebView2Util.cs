namespace Bloxstrap.Utility
{
    // snitch.out: shared WebView2 runtime detection + install
    static class WebView2Util
    {
        private const string ClientStateSubkey = @"SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
        private const string EvergreenBootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        public static string? GetVersion()
        {
            foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
            {
                using var key = root.OpenSubKey(ClientStateSubkey);

                if (key?.GetValue("pv") is string version && !String.IsNullOrEmpty(version))
                    return version;
            }

            return null;
        }

        public static bool IsInstalled() => GetVersion() is not null;

        public static async Task<string?> DownloadInstallerAsync()
        {
            const string LOG_IDENT = "WebView2Util::DownloadInstaller";

            try
            {
                string path = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

                using var response = await App.HttpClient.GetAsync(EvergreenBootstrapperUrl);
                response.EnsureSuccessStatusCode();

                await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fileStream);

                return path;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return null;
            }
        }
    }
}
