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

        public static async Task<long?> ResolveUserIdAsync(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim();

            // profile URL carries the id: /users/{id}/...
            var urlMatch = Regex.Match(input, @"/users/(\d+)");

            if (urlMatch.Success && long.TryParse(urlMatch.Groups[1].Value, out long urlId))
                return urlId;

            if (long.TryParse(input, out long numericId))
                return numericId;

            // plain username
            var payload = new StringContent(JsonSerializer.Serialize(new
            {
                usernames = new[] { input },
                excludeBannedUsers = false
            }));

            var response = await Http.SendJson<UsernameResolveResponse>(new HttpRequestMessage
            {
                RequestUri = new Uri("https://users.roblox.com/v1/usernames/users"),
                Method = HttpMethod.Post,
                Content = payload
            });

            return response?.Data.FirstOrDefault()?.Id;
        }

        public static async Task<Models.RobloxApi.GetUserResponse?> GetUserAsync(long userId)
        {
            return await Http.GetJson<Models.RobloxApi.GetUserResponse>(new Uri($"https://users.roblox.com/v1/users/{userId}"));
        }

        public static async Task<int?> GetFriendCountAsync(long userId)
        {
            try
            {
                var response = await Http.GetJson<FriendCountResponse>(
                    new Uri($"https://friends.roblox.com/v1/users/{userId}/friends/count"));

                return response?.Count;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string?> GetAvatarAsync(long userId, CancellationToken token)
        {
            return await Thumbnails.GetThumbnailUrlAsync(new ThumbnailRequest
            {
                TargetId = (ulong)userId,
                Type = "AvatarHeadshot",
                Size = "150x150",
                Format = "Png",
                IsCircular = false
            }, token);
        }
    }
}
