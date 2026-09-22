using System.Windows;
using Bloxstrap.Models.RobloxApi;
using DiscordRPC;

namespace Bloxstrap.Integrations
{
    public class DiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient = new(App.DiscordAppId);
        private readonly ActivityWatcher? _activityWatcher;
        private readonly Queue<Message> _messageQueue = new();

        // snitch.out: studio mode, details prefix, playtime
        private readonly bool _studioMode;
        private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
        private DateTime _playStartUtc = DateTime.UtcNow;
        private string _statusBase = "";
        private Timer? _refreshTimer;

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private FixedSizeList<ThumbnailCacheEntry> _thumbnailCache = new FixedSizeList<ThumbnailCacheEntry>(20);

        private ulong? _smallImgBeingFetched = null;
        private ulong? _largeImgBeingFetched = null;
        private CancellationTokenSource? _fetchThumbnailsToken;

        private bool _visible = true;

        public DiscordRichPresence(ActivityWatcher? activityWatcher, bool studioMode = false)
        {
            const string LOG_IDENT = "DiscordRichPresence";

            _activityWatcher = activityWatcher;
            _studioMode = studioMode;

            if (_activityWatcher is not null)
            {
                _activityWatcher.OnGameJoin += (_, _) => Task.Run(() => SetCurrentGame());
                _activityWatcher.OnGameLeave += (_, _) => Task.Run(() => SetCurrentGame());
                _activityWatcher.OnRPCMessage += (_, message) => ProcessRPCMessage(message);
            }

            if (_studioMode)
                Task.Run(() => SetStudioPresence());

            _rpcClient.OnReady += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Received ready from user {e.User} ({e.User.ID})");

            _rpcClient.OnPresenceUpdate += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Presence updated");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"An RPC error occurred - {e.Message}");

            _rpcClient.OnConnectionEstablished += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, "Established connection with Discord RPC");

            //spams log as it tries to connect every ~15 sec when discord is closed so not now
            //_rpcClient.OnConnectionFailed += (_, e) =>
            //    App.Logger.WriteLine(LOG_IDENT, "Failed to establish connection with Discord RPC");

            _rpcClient.OnClose += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Lost connection to Discord RPC - {e.Reason} ({e.Code})");

            _rpcClient.Initialize();
        }

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            const string LOG_IDENT = "DiscordRichPresence::ProcessRPCMessage";

            if (_studioMode)
                return;

            if (message.Command != "SetRichPresence" && message.Command != "SetLaunchData")
                return;

            if (_currentPresence is null || _originalPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Presence is not set, enqueuing message");
                _messageQueue.Enqueue(message);
                return;
            }

            // a lot of repeated code here, could this somehow be cleaned up?

            if (message.Command == "SetLaunchData")
            {
                _currentPresence.Buttons = GetButtons();
            }
            else if (message.Command == "SetRichPresence")
            {
                ProcessSetRichPresence(message, implicitUpdate);
            }

            if (implicitUpdate)
                UpdatePresence();
        }

        private void AddToThumbnailCache(ulong id, string? url)
        {
            if (url != null)
                _thumbnailCache.Add(new ThumbnailCacheEntry { Id = id, Url = url });
        }

        private async Task UpdatePresenceIconsAsync(ulong? smallImg, ulong? largeImg, bool implicitUpdate, CancellationToken token)
        {
            Debug.Assert(smallImg != null || largeImg != null);

            if (smallImg != null && largeImg != null)
            {
                string?[] urls = await Thumbnails.GetThumbnailUrlsAsync(new List<ThumbnailRequest>
                {
                    new ThumbnailRequest
                    {
                        TargetId = (ulong)smallImg,
                        Type = "Asset",
                        Size = "512x512",
                        IsCircular = false
                    },
                    new ThumbnailRequest
                    {
                        TargetId = (ulong)largeImg,
                        Type = "Asset",
                        Size = "512x512",
                        IsCircular = false
                    }
                }, token);

                string? smallUrl = urls[0];
                string? largeUrl = urls[1];

                AddToThumbnailCache((ulong)smallImg, smallUrl);
                AddToThumbnailCache((ulong)largeImg, largeUrl);

                if (_currentPresence != null)
                {
                    _currentPresence.Assets.SmallImageKey = smallUrl;
                    _currentPresence.Assets.LargeImageKey = largeUrl;
                }
            }
            else if (smallImg != null)
            {
                string? url = await Thumbnails.GetThumbnailUrlAsync(new ThumbnailRequest
                {
                    TargetId = (ulong)smallImg,
                    Type = "Asset",
                    Size = "512x512",
                    IsCircular = false
                }, token);

                AddToThumbnailCache((ulong)smallImg, url);

                if (_currentPresence != null)
                    _currentPresence.Assets.SmallImageKey = url;
            }
            else if (largeImg != null)
            {
                string? url = await Thumbnails.GetThumbnailUrlAsync(new ThumbnailRequest
                {
                    TargetId = (ulong)largeImg,
                    Type = "Asset",
                    Size = "512x512",
                    IsCircular = false
                }, token);

                AddToThumbnailCache((ulong)largeImg, url);

                if (_currentPresence != null)
                    _currentPresence.Assets.LargeImageKey = url;
            }

            _smallImgBeingFetched = null;
            _largeImgBeingFetched = null;

            if (implicitUpdate)
                UpdatePresence();
        }

        private void ProcessSetRichPresence(Message message, bool implicitUpdate)
        {
            const string LOG_IDENT = "DiscordRichPresence::ProcessSetRichPresence";
            Models.BloxstrapRPC.RichPresence? presenceData;

            Debug.Assert(_currentPresence is not null);
            Debug.Assert(_originalPresence is not null);

            if (_fetchThumbnailsToken != null)
            {
                _fetchThumbnailsToken.Cancel();
                _fetchThumbnailsToken = null;
            }

            try
            {
                presenceData = message.Data.Deserialize<Models.BloxstrapRPC.RichPresence>();
            }
            catch (Exception)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization threw an exception)");
                return;
            }

            if (presenceData is null)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to parse message! (JSON deserialization returned null)");
                return;
            }

            if (presenceData.Details is not null)
            {
                if (presenceData.Details.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"Details cannot be longer than 128 characters");
                else if (presenceData.Details == "<reset>")
                    _currentPresence.Details = _originalPresence.Details;
                else
                    _currentPresence.Details = presenceData.Details;
            }

            if (presenceData.State is not null)
            {
                if (presenceData.State.Length > 128)
                    App.Logger.WriteLine(LOG_IDENT, $"State cannot be longer than 128 characters");
                else if (presenceData.State == "<reset>")
                    _currentPresence.State = _originalPresence.State;
                else
                    _currentPresence.State = presenceData.State;
            }

            if (presenceData.TimestampStart == 0)
                _currentPresence.Timestamps.Start = null;
            else if (presenceData.TimestampStart is not null)
                _currentPresence.Timestamps.StartUnixMilliseconds = presenceData.TimestampStart * 1000;

            if (presenceData.TimestampEnd == 0)
                _currentPresence.Timestamps.End = null;
            else if (presenceData.TimestampEnd is not null)
                _currentPresence.Timestamps.EndUnixMilliseconds = presenceData.TimestampEnd * 1000;

            // set these to start fetching
            ulong? smallImgFetch = null;
            ulong? largeImgFetch = null;

            // only set small image if account display is disabled, doesnt make sense to override it if it is true
            if (presenceData.SmallImage is not null && !App.Settings.Prop.ShowAccountOnRichPresence)
            {
                if (presenceData.SmallImage.Clear)
                {
                    _currentPresence.Assets.SmallImageKey = "";
                    _smallImgBeingFetched = null;
                }
                else if (presenceData.SmallImage.Reset)
                {
                    _currentPresence.Assets.SmallImageText = _originalPresence.Assets.SmallImageText;
                    _currentPresence.Assets.SmallImageKey = _originalPresence.Assets.SmallImageKey;
                    _smallImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.SmallImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.SmallImage.AssetId);

                        if (entry == null)
                        {
                            smallImgFetch = presenceData.SmallImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.SmallImageKey = entry.Url;
                            _smallImgBeingFetched = null;
                        }
                    }

                    if (presenceData.SmallImage.HoverText is not null)
                        _currentPresence.Assets.SmallImageText = presenceData.SmallImage.HoverText;
                }
            }

            if (presenceData.LargeImage is not null)
            {
                if (presenceData.LargeImage.Clear)
                {
                    _currentPresence.Assets.LargeImageKey = "";
                    _largeImgBeingFetched = null;
                }
                else if (presenceData.LargeImage.Reset)
                {
                    _currentPresence.Assets.LargeImageText = _originalPresence.Assets.LargeImageText;
                    _currentPresence.Assets.LargeImageKey = _originalPresence.Assets.LargeImageKey;
                    _largeImgBeingFetched = null;
                }
                else
                {
                    if (presenceData.LargeImage.AssetId is not null)
                    {
                        ThumbnailCacheEntry? entry = _thumbnailCache.FirstOrDefault(x => x.Id == presenceData.LargeImage.AssetId);

                        if (entry == null)
                        {
                            largeImgFetch = presenceData.LargeImage.AssetId;
                        }
                        else
                        {
                            _currentPresence.Assets.LargeImageKey = entry.Url;
                            _largeImgBeingFetched = null;
                        }
                    }

                    if (presenceData.LargeImage.HoverText is not null)
                        _currentPresence.Assets.LargeImageText = presenceData.LargeImage.HoverText;
                }
            }

            if (smallImgFetch != null)
                _smallImgBeingFetched = smallImgFetch;
            if (largeImgFetch != null)
                _largeImgBeingFetched = largeImgFetch;

            if (_smallImgBeingFetched != null || _largeImgBeingFetched != null)
            {
                _fetchThumbnailsToken = new CancellationTokenSource();
                Task.Run(() => UpdatePresenceIconsAsync(_smallImgBeingFetched, _largeImgBeingFetched, implicitUpdate, _fetchThumbnailsToken.Token));
            }
        }

        public void SetVisibility(bool visible)
        {
            App.Logger.WriteLine("DiscordRichPresence::SetVisibility", $"Setting presence visibility ({visible})");

            _visible = visible;

            if (_visible)
                UpdatePresence();
            else
                _rpcClient.ClearPresence();
        }

        public async Task<bool> SetCurrentGame()
        {
            const string LOG_IDENT = "DiscordRichPresence::SetCurrentGame";

            if (_activityWatcher is null)
                return false;

            if (!_activityWatcher.InGame)
            {
                App.Logger.WriteLine(LOG_IDENT, "Not in game, clearing presence");

                _currentPresence = _originalPresence =  null;
                _messageQueue.Clear();

                UpdatePresence();
                return true;
            }

            string icon = "roblox";
            string smallImageText = "Roblox";
            string smallImage = "roblox";
            

            var activity = _activityWatcher.Data;
            long placeId = activity.PlaceId;

            App.Logger.WriteLine(LOG_IDENT, $"Setting presence for Place ID {placeId}");

            // preserve time spent playing if we're teleporting between places in the same universe
            var timeStarted = activity.TimeJoined;

            if (activity.RootActivity is not null)
                timeStarted = activity.RootActivity.TimeJoined;

            if (activity.UniverseDetails is null)
            {
                try
                {
                    await UniverseDetails.FetchSingle(activity.UniverseId);
                }
                catch (InvalidHTTPResponseException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Universe {activity.UniverseId} is restricted");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    return false;
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException(LOG_IDENT, ex);
                    Frontend.ShowMessageBox($"{Strings.ActivityWatcher_RichPresenceLoadFailed}\n\n{ex.Message}", MessageBoxImage.Warning);
                    return false;
                }

                activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
            }

            var universeDetails = activity.UniverseDetails!;

            icon = universeDetails.Thumbnail.ImageUrl!;

            if (App.Settings.Prop.ShowAccountOnRichPresence)
            {
                var userDetails = await UserDetails.Fetch(activity.UserId);

                smallImage = userDetails.Thumbnail.ImageUrl!;
                smallImageText = $"Playing on {userDetails.Data.DisplayName} (@{userDetails.Data.Name})"; // i.e. "axell (@Axelan_se)"
            }

            if (!_activityWatcher.InGame || placeId != activity.PlaceId)
            {
                App.Logger.WriteLine(LOG_IDENT, "Aborting presence set because game activity has changed");
                return false;
            }

            string status = _activityWatcher.Data.ServerType switch
            {
                ServerType.Private => "In a private server",
                ServerType.Reserved => "In a reserved server",
                _ => $"by {universeDetails.Data.Creator.Name}" + (universeDetails.Data.Creator.HasVerifiedBadge ? " ☑️" : ""),
            };

            string universeName = universeDetails.Data.Name;

            if (universeName.Length < 2)
                universeName = $"{universeName}\x2800\x2800\x2800";

            // snitch.out: optional details prefix + playtime in state
            _playStartUtc = timeStarted.ToUniversalTime();
            _statusBase = status;

            _currentPresence = new DiscordRPC.RichPresence
            {
                Details = Truncate(App.Settings.Prop.RichPresenceDetailsPrefix + universeName, 128),
                StatusDisplay = App.Settings.Prop.RichPresenceStatusDisplayType.ToStatusDisplayType(),
                State = Truncate(BuildStateText(status, _playStartUtc), 128),
                Timestamps = new Timestamps { Start = timeStarted.ToUniversalTime() },
                Buttons = GetButtons(),
                Assets = new Assets
                {
                    LargeImageKey = icon,
                    LargeImageText = universeDetails.Data.Name,
                    SmallImageKey = smallImage,
                    SmallImageText = smallImageText
                }
            };

            // this is used for configuration from BloxstrapRPC
            _originalPresence = _currentPresence.Clone();

            if (_messageQueue.Any())
            {
                App.Logger.WriteLine(LOG_IDENT, "Processing queued messages");
                ProcessRPCMessage(_messageQueue.Dequeue(), false);
            }

            StartRefreshTimer();
            UpdatePresence();

            return true;
        }

        // snitch.out: static presence for Roblox Studio (no log parsing there)
        public void SetStudioPresence()
        {
            const string LOG_IDENT = "DiscordRichPresence::SetStudioPresence";

            App.Logger.WriteLine(LOG_IDENT, "Setting Studio presence");

            _playStartUtc = _sessionStartUtc;
            _statusBase = "Building";

            _currentPresence = new DiscordRPC.RichPresence
            {
                Details = Truncate(App.Settings.Prop.RichPresenceDetailsPrefix + "Roblox Studio", 128),
                StatusDisplay = App.Settings.Prop.RichPresenceStatusDisplayType.ToStatusDisplayType(),
                State = Truncate(BuildStateText(_statusBase, _playStartUtc), 128),
                Timestamps = new Timestamps { Start = _sessionStartUtc },
                Buttons = GetButtons(),
                Assets = new Assets
                {
                    LargeImageKey = "roblox",
                    LargeImageText = "Roblox Studio",
                    SmallImageKey = "roblox",
                    SmallImageText = "snitch.out"
                }
            };

            _originalPresence = _currentPresence.Clone();

            StartRefreshTimer();
            UpdatePresence();
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
                return $"{(int)span.TotalHours}h {span.Minutes}m";

            if (span.TotalMinutes >= 1)
                return $"{(int)span.TotalMinutes}m";

            return $"{(int)span.TotalSeconds}s";
        }

        private string BuildStateText(string status, DateTime playStartUtc)
        {
            if (!App.Settings.Prop.ShowPlaytimeOnPresence)
                return status;

            TimeSpan session = DateTime.UtcNow - playStartUtc;
            TimeSpan total = TimeSpan.FromSeconds(App.State.Prop.TotalPlaytimeSeconds) + session;

            return $"{status} · {FormatDuration(session)} session ({FormatDuration(total)} total)";
        }

        private void StartRefreshTimer()
        {
            if (!App.Settings.Prop.ShowPlaytimeOnPresence)
                return;

            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(_ =>
            {
                if (_currentPresence is null || !_visible)
                    return;

                _currentPresence.State = Truncate(BuildStateText(_statusBase, _playStartUtc), 128);
                _rpcClient.SetPresence(_currentPresence);
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public Button[] GetButtons()
        {
            // snitch.out: studio has no joinable server, link the project
            if (_studioMode || _activityWatcher is null)
            {
                return new Button[]
                {
                    new Button
                    {
                        Label = "snitch.out on GitHub",
                        Url = "https://github.com/7yass/snitch.out"
                    }
                };
            }

            var buttons = new List<Button>();

            var data = _activityWatcher.Data;

            if (!App.Settings.Prop.HideRPCButtons)
            {
                bool show = false;

                if (data.ServerType == ServerType.Public)
                    show = true;
                else if (data.ServerType == ServerType.Reserved && !String.IsNullOrEmpty(data.RPCLaunchData))
                    show = true;

                if (show)
                {
                    buttons.Add(new Button
                    {
                        Label = "Join server",
                        Url = data.GetInviteDeeplink(true, true)
                    });
                }
            }

            buttons.Add(new Button
            {
                Label = "See game page",
                Url = $"https://www.roblox.com/games/{data.PlaceId}"
            });

            return buttons.ToArray();
        }

        public void UpdatePresence()
        {
            const string LOG_IDENT = "DiscordRichPresence::UpdatePresence";
            
            if (_currentPresence is null)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Presence is empty, clearing");
                _rpcClient.ClearPresence();
                return;
            }

            App.Logger.WriteLine(LOG_IDENT, $"Updating presence");

            if (_visible)
                _rpcClient.SetPresence(_currentPresence);
        }

        public void Dispose()
        {
            App.Logger.WriteLine("DiscordRichPresence::Dispose", "Cleaning up Discord RPC and Presence");
            _refreshTimer?.Dispose();
            _rpcClient.ClearPresence();
            _rpcClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
