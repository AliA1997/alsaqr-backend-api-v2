namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Cloudflare Calls (Realtime SFU) settings, bound from the "CloudflareCalls"
    /// configuration section. The app secret is server-side only: every SFU
    /// interaction is proxied through <c>ICloudflareCallsService</c> and the
    /// secret must never appear in a response, log line, or client-visible error
    /// (specs/audio-spaces.md).
    /// </summary>
    public sealed class CloudflareCallsConfig
    {
        public string AppId { get; set; } = default!;
        public string AppSecret { get; set; } = default!;

        /// <summary>e.g. https://rtc.live.cloudflare.com/v1</summary>
        public string BaseUrl { get; set; } = "https://rtc.live.cloudflare.com/v1";
    }
}
