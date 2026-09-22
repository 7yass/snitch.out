using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class PlaytimeEntry
    {
        public string GameName { get; set; } = "";
        public int Sessions { get; set; }
        public string TotalTime { get; set; } = "";
        public string LastPlayed { get; set; } = "";
    }

    public class PlaytimeViewModel : NotifyPropertyChangedViewModel
    {
        private string _robuxText = "";
        private bool _loadingBalance = false;

        public ObservableCollection<PlaytimeEntry> Games { get; } = new();

        public string TotalsText { get; private set; } = "";

        public string RobuxText
        {
            get => _robuxText;
            private set
            {
                _robuxText = value;
                OnPropertyChanged(nameof(RobuxText));
            }
        }

        public bool CanLoadBalance => App.Cookies.Loaded && !_loadingBalance;

        public bool NeedsCookieAccess => !App.Cookies.Loaded;

        public ICommand RefreshBalanceCommand => new RelayCommand(async () => await LoadBalance());

        public PlaytimeViewModel()
        {
            ReloadGames();
            App.Cookies.StateChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanLoadBalance));
                OnPropertyChanged(nameof(NeedsCookieAccess));
            };
        }

        public void ReloadGames()
        {
            Games.Clear();

            var games = App.Playtime.Games.Values
                .OrderByDescending(x => x.TotalSeconds)
                .ToList();

            foreach (var game in games)
            {
                Games.Add(new PlaytimeEntry
                {
                    GameName = String.IsNullOrEmpty(game.GameName) ? $"Universe {game.UniverseId}" : game.GameName,
                    Sessions = game.Sessions,
                    TotalTime = Utility.Time.FormatTimeSpan(TimeSpan.FromSeconds(game.TotalSeconds)),
                    LastPlayed = game.LastPlayed.ToString("g")
                });
            }

            long totalSeconds = games.Sum(x => x.TotalSeconds);
            int sessions = games.Sum(x => x.Sessions);

            TotalsText = games.Count == 0
                ? "No tracked sessions yet. Play something and it will show up here."
                : $"{games.Count} games · {sessions} sessions · {Utility.Time.FormatTimeSpan(TimeSpan.FromSeconds(totalSeconds))} total";

            OnPropertyChanged(nameof(TotalsText));
        }

        private async Task LoadBalance()
        {
            if (!App.Cookies.Loaded)
                return;

            _loadingBalance = true;
            OnPropertyChanged(nameof(CanLoadBalance));
            RobuxText = "Loading...";

            try
            {
                using var response = await App.Cookies.AuthGet(new Uri("https://economy.roblox.com/v1/user/currency"));
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                var balance = JsonSerializer.Deserialize<RobuxBalance>(content);

                RobuxText = balance is null ? "Failed to read balance." : $"R$ {balance.Robux:N0}";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("PlaytimeViewModel::LoadBalance", ex);
                RobuxText = "Failed to load balance.";
            }

            _loadingBalance = false;
            OnPropertyChanged(nameof(CanLoadBalance));
        }

        private sealed class RobuxBalance
        {
            [JsonPropertyName("robux")]
            public long Robux { get; set; }
        }
    }
}
