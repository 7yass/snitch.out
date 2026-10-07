using System.Windows;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Bloxstrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Froststrap-style account login: log in inside the embedded browser,
    /// the .ROBLOSECURITY cookie is captured into the account vault.
    /// </summary>
    public partial class AccountLoginDialog
    {
        private const string LoginUrl = "https://www.roblox.com/login";

        private string? _capturedCookie;

        public bool Saved { get; private set; } = false;

        public AccountLoginDialog()
        {
            InitializeComponent();

            Browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Paths.Base, "WebView2")
            };

            this.Loaded += AccountLoginDialog_Loaded;
        }

        private async void AccountLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Browser.EnsureCoreWebView2Async();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is FileNotFoundException || ex.Message.Contains("runtime", StringComparison.OrdinalIgnoreCase))
            {
                App.Logger.WriteException("AccountLoginDialog", ex);
                StatusText.Text = "WebView2 runtime is missing, so the login browser cannot open. Install Roblox once (it ships WebView2) and try again.";
                return;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AccountLoginDialog", ex);
                StatusText.Text = $"Login browser failed to start: {ex.Message}";
                return;
            }

            Browser.NavigationCompleted += async (_, _) => await RefreshLoginStateAsync();
            Browser.Source = new Uri(LoginUrl);
        }

        private async Task RefreshLoginStateAsync()
        {
            if (Browser.CoreWebView2 is null)
                return;

            try
            {
                var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
                var auth = cookies.FirstOrDefault(x => x.Name == ".ROBLOSECURITY");

                if (auth is null || String.IsNullOrEmpty(auth.Value))
                {
                    _capturedCookie = null;
                    SaveButton.IsEnabled = false;
                    StatusText.Text = "Log in above. The save button unlocks once your session is detected.";
                    return;
                }

                _capturedCookie = auth.Value;
                SaveButton.IsEnabled = true;
                StatusText.Text = "Session detected. Click Save account to add it to your vault.";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("AccountLoginDialog", ex);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(_capturedCookie))
                return;

            SaveButton.IsEnabled = false;
            StatusText.Text = "Saving account...";

            string? error = await App.AccountVault.ImportCookieAsync(_capturedCookie);

            if (error is not null)
            {
                StatusText.Text = error;
                SaveButton.IsEnabled = true;
                return;
            }

            Saved = true;
            Close();
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (Browser.CoreWebView2 is null)
                return;

            _capturedCookie = null;
            SaveButton.IsEnabled = false;

            await Browser.CoreWebView2.Profile.ClearBrowsingDataAsync();
            Browser.Source = new Uri(LoginUrl);
            StatusText.Text = "Saved login cleared. Log in as a different account to add it.";
        }
    }
}
