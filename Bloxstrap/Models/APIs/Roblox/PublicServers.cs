namespace Bloxstrap.Models.APIs.Roblox
{
    // games.roblox.com/v1/games/{placeId}/servers/Public
    // fields beyond id/playing/maxPlayers are best-effort and may be absent
    public class PublicServerListResponse
    {
        [JsonPropertyName("data")]
        public List<PublicServer> Data { get; set; } = new();

        [JsonPropertyName("nextPageCursor")]
        public string? NextPageCursor { get; set; }
    }

    public class PublicServer
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; }

        [JsonPropertyName("playing")]
        public int Playing { get; set; }

        [JsonPropertyName("fps")]
        public double? Fps { get; set; }

        [JsonPropertyName("ping")]
        public int? Ping { get; set; }

        [JsonPropertyName("players")]
        public List<PublicServerPlayer>? Players { get; set; }
    }

    public class PublicServerPlayer
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
    }
}
