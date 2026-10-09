using System.Text;
using System.Text.Json;

namespace AlSaqr.Tests.Support
{
    /// <summary>Decodes a JWT segment as raw JSON, so claims are asserted exactly as sent.</summary>
    public static class JwtJson
    {
        public static JsonElement Header(string jwt) => Segment(jwt, 0);

        public static JsonElement Payload(string jwt) => Segment(jwt, 1);

        private static JsonElement Segment(string jwt, int index)
        {
            var segment = jwt.Split('.')[index].Replace('-', '+').Replace('_', '/');
            segment = segment.PadRight(segment.Length + (4 - segment.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(segment)));
            return doc.RootElement.Clone();
        }
    }
}
