using Bloxstrap.Models.Entities;

namespace Bloxstrap
{
    // snitch.out: per-game playtime aggregated from activity history
    public class PlaytimeManager
    {
        private const string LOG_IDENT = "PlaytimeManager";

        public Dictionary<string, GamePlaytime> Games { get; private set; } = new();

        public string FileLocation => Path.Combine(Paths.Base, "Playtime.json");

        public void Load()
        {
            try
            {
                if (!File.Exists(FileLocation))
                    return;

                var games = JsonSerializer.Deserialize<Dictionary<string, GamePlaytime>>(File.ReadAllText(FileLocation));

                if (games is not null)
                    Games = games;
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
                Directory.CreateDirectory(Path.GetDirectoryName(FileLocation)!);
                File.WriteAllText(FileLocation, JsonSerializer.Serialize(Games, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public void RecordLeave(ActivityData session)
        {
            if (session.UniverseId == 0 || !session.TimeLeft.HasValue)
                return;

            long seconds = (long)(session.TimeLeft.Value - session.TimeJoined).TotalSeconds;

            // ignore log noise and failed joins
            if (seconds < 5)
                return;

            string key = session.UniverseId.ToString();
            string name = session.UniverseDetails?.Data.Name ?? "";

            if (!Games.TryGetValue(key, out GamePlaytime? entry))
            {
                entry = new GamePlaytime { UniverseId = session.UniverseId };
                Games[key] = entry;
            }

            if (!String.IsNullOrEmpty(name))
                entry.GameName = name;

            entry.TotalSeconds += seconds;
            entry.Sessions++;
            entry.LastPlayed = session.TimeLeft.Value;
        }
    }
}
