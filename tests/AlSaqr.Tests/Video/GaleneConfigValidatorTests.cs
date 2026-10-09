using AlSaqr.Infrastructure.Config;

namespace AlSaqr.Tests.Video
{
    public class GaleneConfigValidatorTests
    {
        private static GaleneConfig Valid() => new()
        {
            AdminUrl = "https://galene.internal:8443",
            PublicUrl = "https://video.alsaqr.app",
            AdminUsername = "admin",
            AdminPassword = "password",
            TokenSecret = "0123456789abcdef0123456789abcdef",
        };

        private static bool Ok(GaleneConfig c) => new GaleneConfigValidator().Validate(null, c).Succeeded;

        [Fact]
        public void ValidConfig_Passes() => Assert.True(Ok(Valid()));

        [Theory]
        [InlineData("AdminUsername")]
        [InlineData("AdminPassword")]
        [InlineData("TokenSecret")]
        public void MissingRequiredValue_Fails(string property)
        {
            var config = Valid();
            typeof(GaleneConfig).GetProperty(property)!.SetValue(config, "");

            Assert.False(Ok(config));
        }

        [Theory]
        [InlineData("short")]
        [InlineData("0123456789abcdef0123456789abcdef0")]
        public void TokenSecret_NotExactly32Bytes_Fails(string secret)
        {
            var config = Valid();
            config.TokenSecret = secret;

            Assert.False(Ok(config));
        }

        [Theory]
        [InlineData("http://localhost:8443")]
        [InlineData("http://127.0.0.1:8443")]
        [InlineData("http://[::1]:8443")]
        [InlineData("https://video.alsaqr.app")]
        public void LoopbackHttp_AndHttps_AreAllowed(string url)
        {
            var config = Valid();
            config.AdminUrl = url;
            config.PublicUrl = url;

            Assert.True(Ok(config));
        }

        [Theory]
        [InlineData("http://galene.internal:8443")]
        [InlineData("http://video.alsaqr.app")]
        [InlineData("not a url")]
        public void NonLoopbackCleartext_Fails_ForEitherUrl(string url)
        {
            var admin = Valid();
            admin.AdminUrl = url;
            var publicUrl = Valid();
            publicUrl.PublicUrl = url;

            Assert.False(Ok(admin));
            Assert.False(Ok(publicUrl));
        }
    }
}
