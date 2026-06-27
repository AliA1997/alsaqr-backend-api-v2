using System.Text.Json;

namespace  AlSaqr.Domain.Utils
{
    public static class Auth
    {
        public class JwtSettings
        {
            public string Secret { get; set; }
            public string Issuer { get; set; }
            public string Audience { get; set; }
            public int ExpiryMinutes { get; set; }
        }

        /// <summary>
        /// Lightweight Bearer access-token gate (see specs/access-token.md). Reads the JWT
        /// from the Authorization header, decodes its payload and rejects the request when
        /// the token is missing or its <c>exp</c> claim is in the past. The token's
        /// signature is intentionally NOT verified here — this is only an authorization
        /// gate; the acting user's identity still comes from the user cache.
        /// </summary>
        public static class AccessTokenValidator
        {
            public static bool IsValid(string? authorizationHeader, out string error)
            {
                error = string.Empty;

                if (string.IsNullOrWhiteSpace(authorizationHeader))
                {
                    error = "Access token is required.";
                    return false;
                }

                var token = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authorizationHeader.Substring("Bearer ".Length).Trim()
                    : authorizationHeader.Trim();

                if (string.IsNullOrWhiteSpace(token))
                {
                    error = "Access token is required.";
                    return false;
                }

                var parts = token.Split('.');
                if (parts.Length < 2)
                {
                    error = "Access token is invalid.";
                    return false;
                }

                long exp;
                try
                {
                    using var payload = JsonDocument.Parse(DecodeBase64Url(parts[1]));
                    if (!payload.RootElement.TryGetProperty("exp", out var expElement)
                        || !expElement.TryGetInt64(out exp))
                    {
                        error = "Access token is invalid.";
                        return false;
                    }
                }
                catch
                {
                    error = "Access token is invalid.";
                    return false;
                }

                if (exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    error = "Access token is expired.";
                    return false;
                }

                return true;
            }

            private static byte[] DecodeBase64Url(string value)
            {
                var normalized = value.Replace('-', '+').Replace('_', '/');
                switch (normalized.Length % 4)
                {
                    case 2: normalized += "=="; break;
                    case 3: normalized += "="; break;
                }
                return Convert.FromBase64String(normalized);
            }
        }
    }
}
