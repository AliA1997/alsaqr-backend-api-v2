namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Google credentials + model settings for calling the Gemini Flash LLM.
    /// Bound from the "GoogleGemini" section of appsettings / user-secrets.
    /// </summary>
    public class GoogleGeminiConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-3-flash-preview";
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    }
}
