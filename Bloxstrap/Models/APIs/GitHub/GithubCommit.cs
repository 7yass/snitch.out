using System.Text.Json.Serialization;

namespace Bloxstrap.Models.APIs.GitHub
{
    public class GithubCommit
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = "";

        [JsonPropertyName("commit")]
        public GithubCommitInfo Commit { get; set; } = new();
    }

    public class GithubCommitInfo
    {
        [JsonPropertyName("author")]
        public GithubCommitAuthor Author { get; set; } = new();

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public class GithubCommitAuthor
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = "";
    }
}
