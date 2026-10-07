using Bloxstrap.Models.APIs.Roblox;

namespace Bloxstrap.Utility
{
    // snitch.out: RoValra-style server intel on free public endpoints
    static class ServerBrowser
    {
        public static long? ParsePlaceId(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return null;

            var match = Regex.Match(input, @"\d+");

            if (match.Success && long.TryParse(match.Value, out long placeId))
                return placeId;

            return null;
        }

        public static async Task<long?> GetUniverseIdAsync(long placeId)
        {
            var response = await Http.GetJson<UniverseIdResponse>(
                new Uri($"https://apis.roblox.com/universes/v1/places/{placeId}/universe"));

            return response?.UniverseId;
        }

        public static async Task<GameDetailResponse?> GetGameAsync(long universeId)
        {
            var response = await Http.GetJson<ApiArrayResponse<GameDetailResponse>>(
                new Uri($"https://games.roblox.com/v1/games?universeIds={universeId}"));

            return response?.Data.FirstOrDefault();
        }

        public static async Task<string?> GetGameIconAsync(long universeId, CancellationToken token)
        {
            return await Thumbnails.GetThumbnailUrlAsync(new ThumbnailRequest
            {
                TargetId = (ulong)universeId,
                Type = "GameIcon",
                Size = "256x256",
                Format = "Png",
                IsCircular = false
            }, token);
        }

        public static async Task<PublicServerListResponse> GetServersAsync(long placeId, string? cursor, string sortOrder, int limit = 100)
        {
            string url = $"https://games.roblox.com/v1/games/{placeId}/servers/Public?sortOrder={sortOrder}&limit={limit}";

            if (!String.IsNullOrEmpty(cursor))
                url += $"&cursor={Uri.EscapeDataString(cursor)}";

            return await Http.GetJson<PublicServerListResponse>(new Uri(url));
        }

        public static string BuildJoinUrl(long placeId, string jobId) =>
            $"roblox://experiences/start?placeId={placeId}&gameInstanceId={jobId}";
    }
}
