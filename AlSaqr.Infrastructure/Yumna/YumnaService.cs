using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AlSaqr.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Yumna
{
    public interface IYumnaService
    {
        /// <summary>
        /// Sends the (already formatted) prompt to the Gemini Flash LLM and returns
        /// the agent's text response.
        /// </summary>
        Task<string> GetAgentResponse(string prompt, CancellationToken ct = default);
    }

    public sealed class YumnaService : IYumnaService
    {
        private readonly HttpClient _httpClient;
        private readonly GoogleGeminiConfig _config;

        public YumnaService(HttpClient httpClient, IOptions<GoogleGeminiConfig> configuration)
        {
            _httpClient = httpClient;
            _config = configuration.Value;
        }

        public async Task<string> GetAgentResponse(string prompt, CancellationToken ct = default)
        {
            var url =
                $"{_config.BaseUrl.TrimEnd('/')}/v1beta/models/{_config.Model}:generateContent";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", _config.ApiKey);
            request.Content = JsonContent.Create(
                new GeminiGenerateContentRequest
                {
                    Contents = new List<GeminiContent>
                    {
                        new GeminiContent
                        {
                            Parts = new List<GeminiPart> { new GeminiPart { Text = prompt } },
                        },
                    },
                }
            );

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var parsed = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(
                cancellationToken: ct
            );

            var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException("Gemini returned an empty response.");

            return text;
        }

        // ----- Gemini generateContent wire models (transport-only; not domain DTOs) -----

        private sealed class GeminiGenerateContentRequest
        {
            [JsonPropertyName("contents")]
            public List<GeminiContent> Contents { get; set; } = new();
        }

        private sealed class GeminiGenerateContentResponse
        {
            [JsonPropertyName("candidates")]
            public List<GeminiCandidate>? Candidates { get; set; }
        }

        private sealed class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }
        }

        private sealed class GeminiContent
        {
            [JsonPropertyName("parts")]
            public List<GeminiPart>? Parts { get; set; }

            [JsonPropertyName("role")]
            public string? Role { get; set; }
        }

        private sealed class GeminiPart
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
    }
}
