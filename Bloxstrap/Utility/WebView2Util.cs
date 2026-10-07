namespace Bloxstrap.Utility
{
    // snitch.out: shared WebView2 runtime detection + install
    // NOTE: do NOT use the EdgeUpdate ClientState GUID alone. A system-wide
    // runtime install does not always leave a "pv" value where we looked,
    // which caused false "missing" reports. The official API checks everywhere.
    static class WebView2Util
    {
        private const string EvergreenBootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        public static string? GetVersion()
        {
            try
            {
                string? version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(null);

                if (!String.IsNullOrEmpty(version))
                    return version;
            }
            catch
            {
                // fall through to registry check
            }

            // fallback: registry, both hives and both views (system installs
            // can live under HKLM 32-bit view on 64-bit Windows)
            string[] subkeys = new[]
            {
                @"SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
                @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
            };

            foreach (var hive in new[] { Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryHive.LocalMachine })
            {
                foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
                {
                    try
                    {
                        using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);

                        foreach (string subkey in subkeys)
                        {
                            using var key = baseKey.OpenSubKey(subkey);

                            if (key?.GetValue("pv") is string version && !String.IsNullOrEmpty(version))
                                return version;
                        }
                    }
                    catch
                    {
                    }
                }
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
