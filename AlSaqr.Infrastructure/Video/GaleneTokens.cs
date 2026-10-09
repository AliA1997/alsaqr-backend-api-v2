using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.RegularExpressions;
using AlSaqr.Infrastructure.Config;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure.Video
{
    /// <summary>
    /// Galène third-party tokens: HS256 JWTs with sub (username), aud (the group
    /// URL), permissions (string array), iat and exp. Galène verifies them against
    /// the room's installed key, matched by kid.
    /// </summary>
    public static class GaleneTokens
    {
        public const string KeyId = "alsaqr";

        /// <summary>The backend's own moderator identity; no room or participant may use it.</summary>
        public const string OperatorUsername = "alsaqr-operator";

        // Room: stream-{guid}. Identity: {streamId}:{userId} (VideoParticipant.SfuIdentityFor).
        private static readonly Regex RoomPattern = new(@"\A[a-z0-9][a-z0-9-]{0,63}\z", RegexOptions.Compiled);
        private static readonly Regex IdentityPattern = new(@"\A[0-9a-f-]{1,40}:[0-9a-f-]{1,40}\z", RegexOptions.Compiled);

        public static void ValidateRoom(string roomName)
        {
            if (roomName == null || !RoomPattern.IsMatch(roomName) || roomName == OperatorUsername)
                throw new ArgumentException("Invalid room name.", nameof(roomName));
        }

        public static void ValidateIdentity(string identity)
        {
            if (identity == null || !IdentityPattern.IsMatch(identity))
                throw new ArgumentException("Invalid participant identity.", nameof(identity));
        }

        public static string Write(
            GaleneConfig config,
            string roomName,
            string username,
            IReadOnlyCollection<string> permissions,
            TimeSpan ttl,
            out DateTime expiresAt)
        {
            ValidateRoom(roomName);

            var now = DateTimeOffset.UtcNow;
            var exp = now.Add(ttl);
            expiresAt = exp.UtcDateTime;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.TokenSecret)) { KeyId = KeyId };
            var header = new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            var payload = new JwtPayload
            {
                { "sub", username },
                { "aud", Audience(config, roomName) },
                { "permissions", permissions.ToArray() },
                { "iat", now.ToUnixTimeSeconds() },
                { "exp", exp.ToUnixTimeSeconds() },
            };

            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        }

        /// <summary>Galène matches aud against "/group/{name}/" on the public host.</summary>
        public static string Audience(GaleneConfig config, string roomName) =>
            $"{config.PublicUrl.TrimEnd('/')}/group/{roomName}/";

        /// <summary>The JWK "k" value for the room key: base64url of the secret's bytes.</summary>
        public static string KeyValue(GaleneConfig config) =>
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(config.TokenSecret));

        /// <summary>http(s)://host → ws(s)://host/ws, Galène's WebSocket endpoint.</summary>
        public static string WebSocketUrl(string baseUrl)
        {
            var uri = new UriBuilder(baseUrl.TrimEnd('/'));
            uri.Scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
            uri.Path = uri.Path.TrimEnd('/') + "/ws";
            return uri.Uri.ToString();
        }
    }
}
