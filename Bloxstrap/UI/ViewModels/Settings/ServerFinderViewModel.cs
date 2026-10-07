using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ServerRow
    {
        public string JobId { get; set; } = "";
        public string ShortId { get; set; } = "";
        public int Playing { get; set; }
        public int MaxPlayers { get; set; }
        public string PlayersText { get; set; } = "";
        public int FillPercent { get; set; }
        public string DetailText { get; set; } = "";
    }

    public class ServerFinderViewModel : NotifyPropertyChangedViewModel
    {
        private string _placeInput = "";
        private string _statusText = "";
        private string _gameTitle = "";
        private string _gameStats = "";
        private string? _cursor;
        private long _placeId;
        private bool _loading = false;

        public ObservableCollection<ServerRow> Servers { get; } = new();

        public ServerRow? SelectedServer { get; set; }

        public string PlaceInput
        {
            get => _placeInput;
            set
            {
                _placeInput = value;
                OnPropertyChanged(nameof(PlaceInput));
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string GameTitle
        {
            get => _gameTitle;
            private set
            {
                _gameTitle = value;
                OnPropertyChanged(nameof(GameTitle));
            }
        }

        public string GameStats
        {
            get => _gameStats;
            private set
            {
                _gameStats = value;
                OnPropertyChanged(nameof(GameStats));
            }
        }

        public bool IsLoading
        {
            get => _loading;
            private set
            {
                _loading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public List<string> SortOptions { get; } = new() { "Most players", "Fewest players", "Not full first" };

        public string SelectedSort { get; set; } = "Most players";

        public bool HasMore => !String.IsNullOrEmpty(_cursor);

        public ICommand SearchCommand => new RelayCommand(async () => await SearchAsync());
        public ICommand LoadMoreCommand => new RelayCommand(async () => await LoadMoreAsync(), () => HasMore && !IsLoading);
        public ICommand JoinCommand => new RelayCommand(JoinSelected);
        public ICommand CopyJobIdCommand => new RelayCommand(CopyJobId);
        public ICommand LookupCommand => new RelayCommand(async () => await LookupAsync());
        public ICommand RejoinLastCommand => new RelayCommand(RejoinLast);

        private static string FormatCount(long value)
        {
            if (value >= 1_000_000)
                return $"{value / 1_000_000.0:0.0}M";

            if (value >= 1_000)
                return $"{value / 1_000.0:0.0}K";

            return value.ToString();
        }

        private async Task SearchAsync()
        {
            long? placeId = Bloxstrap.Utility.ServerBrowser.ParsePlaceId(PlaceInput);

            if (placeId is null)
            {
                StatusText = "Paste a game link or place ID first.";
                return;
            }

            _placeId = placeId.Value;
            _cursor = null;
            Servers.Clear();
            GameTitle = "";
            GameStats = "";
            IsLoading = true;
            StatusText = "Loading game info...";

            try
            {
                long? universeId = await Bloxstrap.Utility.ServerBrowser.GetUniverseIdAsync(_placeId);

                if (universeId is null)
                {
                    StatusText = "Could not resolve that place ID.";
                    return;
                }

                var game = await Bloxstrap.Utility.ServerBrowser.GetGameAsync(universeId.Value);

                if (game is null)
                {
                    StatusText = "Game not found.";
                    return;
                }

                GameTitle = game.Name;
                GameStats = $"by {game.Creator?.Name} · {FormatCount(game.Playing)} playing · {FormatCount(game.Visits)} visits · {FormatCount(game.FavoritedCount)} favorites";

                await LoadServersAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerFinder::Search", ex);
                StatusText = $"Failed to load: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasMore));
            }
        }

        private async Task LoadMoreAsync()
        {
            if (!HasMore || IsLoading)
                return;

            IsLoading = true;

            try
            {
                await LoadServersAsync();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerFinder::LoadMore", ex);
                StatusText = $"Failed to load servers: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasMore));
            }
        }

        private async Task LoadServersAsync()
        {
            string sortOrder = SelectedSort == "Fewest players" ? "Asc" : "Desc";

            var response = await Bloxstrap.Utility.ServerBrowser.GetServersAsync(_placeId, _cursor, sortOrder);
            _cursor = response.NextPageCursor;

            var rows = response.Data
                .Where(s => !String.IsNullOrEmpty(s.Id) && s.MaxPlayers > 0)
                .Select(s => new ServerRow
                {
                    JobId = s.Id,
                    ShortId = s.Id.Length > 8 ? s.Id[..8] : s.Id,
                    Playing = s.Playing,
                    MaxPlayers = s.MaxPlayers,
                    PlayersText = $"{s.Playing}/{s.MaxPlayers}",
                    FillPercent = (int)Math.Round(s.Playing * 100.0 / s.MaxPlayers),
                    DetailText = s.Players is { Count: > 0 }
                        ? String.Join(", ", s.Players.Take(5).Select(p => String.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName))
                        : (s.Ping.HasValue ? $"{s.Ping}ms" : "")
                });

            if (SelectedSort == "Not full first")
                rows = rows.OrderBy(r => r.Playing >= r.MaxPlayers).ThenByDescending(r => r.Playing);

            foreach (var row in rows)
                Servers.Add(row);

            StatusText = Servers.Count == 0
                ? "No public servers found."
                : $"{Servers.Count} servers listed{(HasMore ? " (more available)" : "")}.";

            OnPropertyChanged(nameof(HasMore));
        }

        private void JoinSelected()
        {
            if (SelectedServer is null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Bloxstrap.Utility.ServerBrowser.BuildJoinUrl(_placeId, SelectedServer.JobId),
                    UseShellExecute = true
                });

                StatusText = $"Joining {SelectedServer.ShortId}...";
            }
            catch (Exception ex)
            {
                StatusText = $"Could not launch: {ex.Message}";
            }
        }

        private void CopyJobId()
        {
            if (SelectedServer is null)
                return;

            Clipboard.SetText(SelectedServer.JobId);
            StatusText = "Server ID copied.";
        }

        // snitch.out: rejoin card
        public bool HasLastSession => App.State.Prop.LastPlaceId != 0 && !String.IsNullOrEmpty(App.State.Prop.LastJobId);

        public Visibility LastSessionVisibility => HasLastSession ? Visibility.Visible : Visibility.Collapsed;

        public string LastSessionText
        {
            get
            {
                string name = String.IsNullOrEmpty(App.State.Prop.LastUniverseName)
                    ? $"Place {App.State.Prop.LastPlaceId}"
                    : App.State.Prop.LastUniverseName;

                string when = App.State.Prop.LastPlayedUtc == default
                    ? ""
                    : $" · last played {App.State.Prop.LastPlayedUtc.ToLocalTime():g}";

                return $"{name}{when}";
            }
        }

        private void RejoinLast()
        {
            if (!HasLastSession)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Bloxstrap.Utility.ServerBrowser.BuildJoinUrl(App.State.Prop.LastPlaceId, App.State.Prop.LastJobId),
                    UseShellExecute = true
                });

                StatusText = "Rejoining last server...";
            }
            catch (Exception ex)
            {
                StatusText = $"Could not launch: {ex.Message}";
            }
        }

        // snitch.out: player lookup
        public string LookupInput { get; set; } = "";

        public string LookupStatus { get; private set; } = "";

        public string LookupName { get; private set; } = "";

        public string LookupDetails { get; private set; } = "";

        public string? LookupAvatarUrl { get; private set; }

        public Visibility LookupVisibility { get; private set; } = Visibility.Collapsed;

        private void SetLookup(string? status = null, string? name = null, string? details = null, string? avatar = null, bool visible = false)
        {
            if (status is not null)
            {
                LookupStatus = status;
                OnPropertyChanged(nameof(LookupStatus));
            }

            LookupName = name ?? "";
            LookupDetails = details ?? "";
            LookupAvatarUrl = avatar;
            LookupVisibility = visible ? Visibility.Visible : Visibility.Collapsed;

            OnPropertyChanged(nameof(LookupName));
            OnPropertyChanged(nameof(LookupDetails));
            OnPropertyChanged(nameof(LookupAvatarUrl));
            OnPropertyChanged(nameof(LookupVisibility));
        }

        private async Task LookupAsync()
        {
            SetLookup(status: "Looking up...");
            OnPropertyChanged(nameof(LastSessionVisibility));

            try
            {
                long? userId = await Bloxstrap.Utility.ServerBrowser.ResolveUserIdAsync(LookupInput);

                if (userId is null)
                {
                    SetLookup(status: "No Roblox user matches that input.");
                    return;
                }

                var profile = await Bloxstrap.Utility.ServerBrowser.GetUserAsync(userId.Value);

                if (profile is null)
                {
                    SetLookup(status: "Profile not found.");
                    return;
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                string? avatar = await Bloxstrap.Utility.ServerBrowser.GetAvatarAsync(userId.Value, cts.Token);
                int? friends = await Bloxstrap.Utility.ServerBrowser.GetFriendCountAsync(userId.Value);

                string name = $"{profile.DisplayName} (@{profile.Name}){(profile.HasVerifiedBadge ? " ☑️" : "")}";
                string details = $"ID {profile.Id} · joined {profile.Created:d} · {(friends.HasValue ? $"{friends.Value} friends" : "friends hidden")}" +
                    (profile.IsBanned ? " · BANNED" : "") +
                    (String.IsNullOrWhiteSpace(profile.Description) ? "" : $"\n{profile.Description.Trim()}");

                SetLookup(status: "", name: name, details: details, avatar: avatar, visible: true);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("ServerFinder::Lookup", ex);
                SetLookup(status: $"Lookup failed: {ex.Message}");
            }
        }
    }
}
