namespace Bloxstrap.Models
{
    public class GamePlaytime
    {
        public long UniverseId { get; set; }
        public string GameName { get; set; } = "";
        public long TotalSeconds { get; set; }
        public int Sessions { get; set; }
        public DateTime LastPlayed { get; set; }
    }
}
