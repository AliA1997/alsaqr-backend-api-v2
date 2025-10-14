using System.Text.Json.Serialization;

namespace AlSaqr.API.Utils
{
    public static class Explore
    {
        public class NewsApiResponse
        {
            [JsonPropertyName("articles")]
            public List<Article> Articles { get; set; } = new();
        }

        public class Article
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;

            [JsonPropertyName("urlToImage")]
            public string? UrlToImage { get; set; }
        }

        public class ExploreToDisplay
        {
            public string Title { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string? UrlToImage { get; set; }
        }

    }
}
