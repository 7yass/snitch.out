namespace Bloxstrap.Models
{
    public class FastFlag
    {
        // public bool Enabled { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;
        // snitch.out: advisory allowlist status, see FastFlagManager
        public string Status { get; set; } = "";
    }
}
