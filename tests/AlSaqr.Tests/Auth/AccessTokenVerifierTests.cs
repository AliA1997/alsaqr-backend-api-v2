using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlSaqr.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AlSaqr.Tests.Auth
{
    public class AccessTokenVerifierTests
    {
        private const string Secret = "test-access-token-secret-0123456789-abcdefghij";
        private readonly AccessTokenVerifier _verifier = new(Secret);

        private static string Issue(string userId) =>
            new TokenService(Secret).GenerateTokens(userId).AccessToken;

        [Fact]
        public void TokenServiceToken_ReturnsUserId()
        {
            var userId = Guid.NewGuid();

            Assert.Equal(userId, _verifier.GetVerifiedUserId($"Bearer {Issue(userId.ToString())}"));
        }

        [Fact]
        public void RawTokenWithoutBearerPrefix_ReturnsUserId()
        {
            var userId = Guid.NewGuid();

            Assert.Equal(userId, _verifier.GetVerifiedUserId(Issue(userId.ToString())));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Bearer ")]
        [InlineData("Bearer not-a-jwt")]
        public void MissingOrMalformed_ReturnsEmpty(string? header)
        {
            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId(header));
        }

        [Fact]
        public void OtherSecret_ReturnsEmpty()
        {
            var forged = new TokenService("a-completely-different-secret-0123456789-xyz")
                .GenerateTokens(Guid.NewGuid().ToString()).AccessToken;

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {forged}"));
        }

        [Fact]
        public void TamperedPayload_ReturnsEmpty()
        {
            var parts = Issue(Guid.NewGuid().ToString()).Split('.');
            var forgedPayload = Base64Url(
                $"{{\"nameid\":\"{Guid.NewGuid()}\",\"exp\":{DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()}}}");

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {parts[0]}.{forgedPayload}.{parts[2]}"));
        }

        [Fact]
        public void UnsignedToken_ReturnsEmpty()
        {
            var header = Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
            var payload = Base64Url(
                $"{{\"nameid\":\"{Guid.NewGuid()}\",\"exp\":{DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()}}}");

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {header}.{payload}."));
        }

        [Fact]
        public void ExpiredToken_ReturnsEmpty()
        {
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)), SecurityAlgorithms.HmacSha256);
            var expired = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                claims: new[] { new Claim("nameid", Guid.NewGuid().ToString()) },
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: DateTime.UtcNow.AddHours(-1),
                signingCredentials: credentials));

            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {expired}"));
        }

        [Fact]
        public void NonGuidUserId_ReturnsEmpty()
        {
            Assert.Equal(Guid.Empty, _verifier.GetVerifiedUserId($"Bearer {Issue("not-a-guid")}"));
        }

        private static string Base64Url(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
