using Newtonsoft.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace  AlSaqr.Domain.SocialMedia
{
    public static class Session
    {
        public class SessionCheckRequest
        {
            public string Email { get; set; }
        }


        public class OAuthUserProfile
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("aud")]
            public string? Aud { get; set; }

            [JsonPropertyName("role")]
            public string? Role { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("email_confirmed_at")]
            public DateTimeOffset? EmailConfirmedAt { get; set; }

            [JsonPropertyName("phone")]
            public string? Phone { get; set; }

            [JsonPropertyName("confirmed_at")]
            public DateTimeOffset? ConfirmedAt { get; set; }

            [JsonPropertyName("last_sign_in_at")]
            public DateTimeOffset? LastSignInAt { get; set; }

            [JsonPropertyName("app_metadata")]
            public OAuthAppMetadata? AppMetadata { get; set; }

            [JsonPropertyName("user_metadata")]
            public OAuthUserMetadata? UserMetadata { get; set; }

            [JsonPropertyName("identities")]
            public List<OAuthIdentity> Identities { get; set; } = new();

            [JsonPropertyName("created_at")]
            public DateTimeOffset? CreatedAt { get; set; }

            [JsonPropertyName("updated_at")]
            public DateTimeOffset? UpdatedAt { get; set; }

            [JsonPropertyName("is_anonymous")]
            public bool IsAnonymous { get; set; }

            // ----- Computed convenience members (not serialized) -----

            /// <summary>The primary provider for this session (e.g. "google").</summary>
            [System.Text.Json.Serialization.JsonIgnore]
            public string? Provider => AppMetadata?.Provider;

            /// <summary>All providers linked to this account (e.g. "google", "discord").</summary>
            [System.Text.Json.Serialization.JsonIgnore]
            public IReadOnlyList<string> LinkedProviders => AppMetadata?.Providers ?? new List<string>();

            [System.Text.Json.Serialization.JsonIgnore]
            public bool IsGoogleAccount => HasProvider("google");

            [System.Text.Json.Serialization.JsonIgnore]
            public bool IsDiscordAccount => HasProvider("discord");

            [System.Text.Json.Serialization.JsonIgnore]
            public bool IsFacebookAccount => HasProvider("facebook");

            /// <summary>Discord's display name, surfaced via user_metadata.custom_claims.global_name.</summary>
            [System.Text.Json.Serialization.JsonIgnore]
            public string? GlobalName => UserMetadata?.CustomClaims?.GlobalName;

            /// <summary>
            /// Best name to show: Discord global_name if present, otherwise full_name / name.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore]
            public string? DisplayName =>
                !string.IsNullOrEmpty(GlobalName)
                    ? GlobalName
                    : UserMetadata?.FullName ?? UserMetadata?.Name;

            /// <summary>Avatar from the primary provider (picture, falling back to avatar_url).</summary>
            [System.Text.Json.Serialization.JsonIgnore]
            public string ProfileAvatar =>
                UserMetadata?.Picture
                ?? UserMetadata?.AvatarUrl
                ?? string.Empty;

            [System.Text.Json.Serialization.JsonIgnore]
            public string EmailUsername => GetEmailUsername(Email);

            // ----- Helpers -----

            public bool HasProvider(string provider) =>
                string.Equals(Provider, provider, StringComparison.OrdinalIgnoreCase)
                || LinkedProviders.Any(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));

            /// <summary>Returns the linked identity for a given provider, if any.</summary>
            public OAuthIdentity? GetIdentity(string provider) =>
                Identities.FirstOrDefault(i =>
                    string.Equals(i.Provider, provider, StringComparison.OrdinalIgnoreCase));

            /// <summary>Pulls the avatar for a specific linked provider from the identities array.</summary>
            public string? GetAvatarForProvider(string provider)
            {
                var data = GetIdentity(provider)?.IdentityData;
                return data?.Picture ?? data?.AvatarUrl;
            }

            public static string GetEmailUsername(string? email) =>
                email?.Split('@').FirstOrDefault() ?? string.Empty;
        }

        public class OAuthAppMetadata
        {
            [JsonPropertyName("provider")]
            public string? Provider { get; set; }

            [JsonPropertyName("providers")]
            public List<string> Providers { get; set; } = new();
        }

        /// <summary>
        /// Shape of both user_metadata and each identity's identity_data — they share the same fields.
        /// </summary>
        public class OAuthUserMetadata
        {
            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }

            [JsonPropertyName("custom_claims")]
            public OAuthCustomClaims? CustomClaims { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("email_verified")]
            public bool? EmailVerified { get; set; }

            [JsonPropertyName("full_name")]
            public string? FullName { get; set; }

            [JsonPropertyName("iss")]
            public string? Iss { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("phone_verified")]
            public bool? PhoneVerified { get; set; }

            [JsonPropertyName("picture")]
            public string? Picture { get; set; }

            [JsonPropertyName("provider_id")]
            public string? ProviderId { get; set; }

            [JsonPropertyName("sub")]
            public string? Sub { get; set; }
        }

        public class OAuthCustomClaims
        {
            [JsonPropertyName("global_name")]
            public string? GlobalName { get; set; }
        }

        public class OAuthIdentity
        {
            [JsonPropertyName("identity_id")]
            public string? IdentityId { get; set; }

            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("user_id")]
            public string? UserId { get; set; }

            [JsonPropertyName("identity_data")]
            public OAuthUserMetadata? IdentityData { get; set; }

            [JsonPropertyName("provider")]
            public string? Provider { get; set; }

            [JsonPropertyName("last_sign_in_at")]
            public DateTimeOffset? LastSignInAt { get; set; }

            [JsonPropertyName("created_at")]
            public DateTimeOffset? CreatedAt { get; set; }

            [JsonPropertyName("updated_at")]
            public DateTimeOffset? UpdatedAt { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }
        }

        /// <summary>Unrelated background-image helper kept from the original file.</summary>
        public static class CityBackgrounds
        {
            public static readonly string[] CityImageUrls =
            {
                "https://images.unsplash.com/photo-1449824913935-59a10b8d2000?ixlib=rb-4.0.3&w=1200", // New York
                "https://images.unsplash.com/photo-1502602898536-47ad22581b52?ixlib=rb-4.0.3&w=1200", // Paris
                "https://images.unsplash.com/photo-1545048702-79362596cdc9?ixlib=rb-4.0.3&w=1200",     // Tokyo
                "https://images.unsplash.com/photo-1512453979798-5ea266f8880c?ixlib=rb-4.0.3&w=1200", // Dubai
                "https://images.unsplash.com/photo-1523531294919-4bcd7c65e216?ixlib=rb-4.0.3&w=1200", // London
                "https://images.unsplash.com/photo-1587474260584-136574528ed5?ixlib=rb-4.0.3&w=1200", // Istanbul
                "https://images.unsplash.com/photo-1542662565-7e4b66bae529?ixlib=rb-4.0.3&w=1200",     // Singapore
                "https://images.unsplash.com/photo-1505881502353-a1986add3762?ixlib=rb-4.0.3&w=1200", // Sydney
                "https://images.unsplash.com/photo-1564500604525-89cbc2baf5d8?ixlib=rb-4.0.3&w=1200", // Hong Kong
                "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?ixlib=rb-4.0.3&w=1200"  // Chicago
            };

            // Random.Shared is thread-safe (.NET 6+) and avoids the repeated-value bug from `new Random()` per call.
           public static string GetRandomCityImage() =>
                CityImageUrls[Random.Shared.Next(CityImageUrls.Length)];
        }
        public static string GetEmailUsername(string email)
        {
            return email?.Split('@').FirstOrDefault() ?? string.Empty;
        }

        public class SessionUser
        {

            public SessionUser(dynamic sessionData)
            {
                this.Id = sessionData.Id;
                this.Username = sessionData.Username;
                this.Avatar = sessionData.Avatar;
                this.BgThumbnail = sessionData.BgThumbnail;
                this.Bio = sessionData.Bio;
                this.CreatedAt = sessionData.CreatedAt;
                this.UpdatedAt = sessionData.UpdatedAt;
                this.CountryOfOrigin = sessionData.CountryOfOrigin;
                this.Email = sessionData.Email;
                this.FirstName = sessionData.FirstName;
                this.LastName = sessionData.LastName;
                this.Phone = sessionData.Phone;
                this.DateOfBirth = sessionData.DateOfBirth;
                this.GeoId = sessionData.GeoId;
                this.MaritalStatus = sessionData.MaritalStatus;
                this.Religion = sessionData.Religion;
                this.PreferredMadhab = sessionData.PreferredMadhab;
                this.FrequentMasjid = sessionData.FrequentMasjid;
                this.Verified = sessionData.Verified;
                this.IsCompleted = sessionData.IsCompleted;
                this.Hobbies = sessionData.Hobbies;
                this.FavoriteQuranReciters = sessionData.FavoriteQuranReciters;
                this.FavoriteIslamicScholars = sessionData.FavoriteIslamicScholars;
                this.IslamicStudyTopics = sessionData.IslamicStudyTopics;

                this.Bookmarks = sessionData.Bookmarks;
                this.Reposts = sessionData.Reposts;
                this.LikedPosts = sessionData.LikedPosts;
                this.Following = sessionData.Following;
                this.FollowingCount = sessionData.FollowingCount;
                this.Followers = sessionData.Followers;
                this.FollowerCount = sessionData.FollowerCount;
            }
            public Guid? Id { get; set; }
            public dynamic? CreatedAt { get; set; }
            public object? UpdatedAt { get; set; }
            public string? Username { get; set; }
            public string? CountryOfOrigin { get; set; }
            public string? Email { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Phone { get; set; }
            public string? Bio { get; set; }
            public string? BgThumbnail { get; set; }
            public string? Avatar { get; set; }
            public dynamic? DateOfBirth { get; set; }
            public string? GeoId { get; set; }
            public string? MaritalStatus { get; set; }
            public string? Religion { get; set; }
            public string? PreferredMadhab { get; set; }
            public dynamic? Hobbies { get; set; }
            public string? FrequentMasjid { get; set; }
            public dynamic? FavoriteQuranReciters { get; set; }
            public dynamic? FavoriteIslamicScholars { get; set; }
            public dynamic? IslamicStudyTopics { get; set; }
            public bool? Verified { get; set; }
            public bool? IsCompleted { get; set; }
            public IDictionary<string, object>[]? Following { get; set; }

            public long FollowingCount { get; set; }

            public IDictionary<string, object>[]? Followers { get; set; }

            public long FollowerCount { get; set; }
            public Guid[] Bookmarks { get; set; }
            public Guid[] Reposts { get; set; }
            public Guid[] LikedPosts { get; set; }
        }
    }
     
}
