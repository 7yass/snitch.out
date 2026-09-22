namespace Bloxstrap.Models
{
    internal class WatcherData
    {
        public int ProcessId { get; set; }

        public string? LogFile { get; set; }

        public List<int>? AutoclosePids { get; set; }

        public long Handle { get; set; }

        // snitch.out: watcher is tracking Studio instead of the player
        public bool IsStudio { get; set; }
    }
}
