namespace Bloxstrap.Models
{
    public class AccountEntry
    {
        public long UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime AddedUtc { get; set; }
    }
}
