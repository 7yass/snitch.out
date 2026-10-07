namespace Bloxstrap
{
    // snitch.out: multi-account vault. Each account keeps a full copy of the
    // Roblox cookie file, so switching is a file swap with zero format risk.
    public class AccountVault
    {
        private const string LOG_IDENT = "AccountVault";

        public List<Models.AccountEntry> Accounts { get; private set; } = new();

        public string VaultDirectory => Path.Combine(Paths.Base, "Accounts");

        private string MetadataFile => Path.Combine(VaultDirectory, "accounts.json");

        private string CookiePathFor(long userId) => Path.Combine(VaultDirectory, $"{userId}.dat");

        public void Load()
        {
            try
            {
                if (!File.Exists(MetadataFile))
                    return;

                var accounts = JsonSerializer.Deserialize<List<Models.AccountEntry>>(File.ReadAllText(MetadataFile));

                if (accounts is not null)
                    Accounts = accounts;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(VaultDirectory);
                File.WriteAllText(MetadataFile, JsonSerializer.Serialize(Accounts, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public bool HasCookie(long userId) => File.Exists(CookiePathFor(userId));

        // user id of the account currently in the live cookie file, if any
        public long? GetCurrentUserId()
        {
            try
            {
                string live = App.Cookies.CookieFilePath;

                if (!File.Exists(live))
                    return null;

                string liveHash = MD5Hash.FromFile(live);

                foreach (var account in Accounts)
                {
                    string vaultFile = CookiePathFor(account.UserId);

                    if (File.Exists(vaultFile) && MD5Hash.FromFile(vaultFile) == liveHash)
                        return account.UserId;
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public async Task<string?> SaveCurrentAsync()
        {
            // snitch.out: the cookie file may have appeared after startup
            // (first Roblox launch), so retry loading before giving up
            if (!App.Cookies.Loaded)
            {
                await App.Cookies.LoadCookies();

                if (!App.Cookies.Loaded)
                    return "Enable account access first, launch Roblox once, then relaunch snitch.out.";
            }

            var user = await App.Cookies.GetAuthenticated();

            if (user is null || user.Id == 0)
                return "Could not verify the logged-in account.";

            string live = App.Cookies.CookieFilePath;

            if (!File.Exists(live))
                return "No Roblox cookie file found. Launch Roblox once first.";

            try
            {
                Directory.CreateDirectory(VaultDirectory);
                File.Copy(live, CookiePathFor(user.Id), true);

                var existing = Accounts.FirstOrDefault(x => x.UserId == user.Id);

                if (existing is null)
                {
                    Accounts.Add(new Models.AccountEntry
                    {
                        UserId = user.Id,
                        DisplayName = user.Displayname,
                        Username = user.Username,
                        AddedUtc = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.DisplayName = user.Displayname;
                    existing.Username = user.Username;
                }

                Save();
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return $"Failed to save account: {ex.Message}";
            }
        }

        public string? SwitchTo(long userId)
        {
            if (Utilities.IsRobloxRunning())
                return "Close Roblox first, then switch accounts.";

            string vaultFile = CookiePathFor(userId);

            if (!File.Exists(vaultFile))
                return "Saved cookie for that account is missing.";

            try
            {
                File.Copy(vaultFile, App.Cookies.CookieFilePath, true);
                Task.Run(App.Cookies.Reload);
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return $"Failed to switch account: {ex.Message}";
            }
        }

        public void Delete(long userId)
        {
            Accounts.RemoveAll(x => x.UserId == userId);

            try
            {
                string vaultFile = CookiePathFor(userId);

                if (File.Exists(vaultFile))
                    File.Delete(vaultFile);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            Save();
        }
    }
}
