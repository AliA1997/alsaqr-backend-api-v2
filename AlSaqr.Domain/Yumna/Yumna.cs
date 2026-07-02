using System.Text.Json.Serialization;

namespace AlSaqr.Domain.Yumna
{
    public static class YumnaAgent
    {
        public class PromptMessageDto
        {
            [JsonPropertyName("prompt")]
            public string Prompt { get; set; }

            [JsonPropertyName("context")]
            public string? Context { get; set; }
        }

        public class YumnaResponseDto
        {
            [JsonPropertyName("result")]
            public string Result { get; set; }

            [JsonPropertyName("dailyUse")]
            public int DailyUse { get; set; }

            [JsonPropertyName("dailyLimit")]
            public int DailyLimit { get; set; }
        }
    }
}
