using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AlSaqr.Infrastructure
{
    /// <summary>
    /// Resolves the acting user from an access token issued by <see cref="TokenService"/>,
    /// verifying its HS256 signature and lifetime first (specs/video-streaming-online-events.md
    /// §0 DEP-2/DEP-3). Auth.AccessTokenValidator only checks presence and expiry, and
    /// UserCacheService keeps one global "loggedInUser" slot, so neither can identify a
    /// caller. Use this wherever an action needs to know who is asking.
    /// </summary>
    public interface IAccessTokenVerifier
    {
        /// <summary>
        /// The verified user id, or Guid.Empty when the header is missing, the token is
        /// malformed, unsigned, signed with another key, expired, or carries no user id.
        /// </summary>
        Guid GetVerifiedUserId(string? authorizationHeader);
    }

    public sealed class AccessTokenVerifier : IAccessTokenVerifier
    {
        // TokenService puts the user id in ClaimTypes.NameIdentifier, which the JWT
        // handler writes under the short claim name "nameid".
        private const string UserIdClaim = "nameid";

        private readonly TokenValidationParameters _parameters;

        public AccessTokenVerifier(string secret)
        {
            _parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                // Same key encoding TokenService signs with.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ValidateLifetime = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }

        public Guid GetVerifiedUserId(string? authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader))
                return Guid.Empty;

            var token = authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorizationHeader["Bearer ".Length..].Trim()
                : authorizationHeader.Trim();

            if (token.Length == 0)
                return Guid.Empty;

            try
            {
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var principal = handler.ValidateToken(token, _parameters, out _);
                return Guid.TryParse(principal.FindFirst(UserIdClaim)?.Value, out var userId)
                    ? userId
                    : Guid.Empty;
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                // Forged, tampered, expired, or malformed: there is no trusted caller.
                return Guid.Empty;
            }
        }
    }
}
