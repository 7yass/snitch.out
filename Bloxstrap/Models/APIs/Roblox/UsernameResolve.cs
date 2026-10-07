namespace Bloxstrap.Models.APIs.Roblox
{
    public class UsernameResolveResponse
    {
        [JsonPropertyName("data")]
        public List<UsernameResolveEntry> Data { get; set; } = new();
    }

    public class UsernameResolveEntry
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";
    }

    public class FriendCountResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
