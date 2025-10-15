using System;
using System.Text.Json;
using System.Text.Json.Serialization;
 

namespace  AlSaqr.Domain.Utils
{
    public static class Session
    {
        public class SessionCheckRequest
        {
            public string Email { get; set; }
        }
        public class OAuthUserProfile
        {
            // Common properties across all providers
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("picture")]
            [JsonIgnore] // We'll handle picture through our custom logic
            public object? Picture { get; set; }

            // Additional image URL property for Discord
            [JsonPropertyName("image_url")]
            public string? ImageUrl { get; set; }

            // Discord-specific properties
            [JsonPropertyName("global_name")]
            public string? GlobalName { get; set; }

            // Google-specific properties
            [JsonPropertyName("given_name")]
            public string? GivenName { get; set; }

            [JsonPropertyName("family_name")]
            public string? FamilyName { get; set; }

            [JsonPropertyName("locale")]
            public string? Locale { get; set; }

            [JsonPropertyName("verified_email")]
            public bool? VerifiedEmail { get; set; }

            // Discord-specific properties
            [JsonPropertyName("username")]
            public string? Username { get; set; }

            [JsonPropertyName("discriminator")]
            public string? Discriminator { get; set; }

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }

            [JsonPropertyName("banner")]
            public string? Banner { get; set; }

            [JsonPropertyName("accent_color")]
            public int? AccentColor { get; set; }

            [JsonPropertyName("verified")]
            public bool? Verified { get; set; }

            // Facebook-specific properties
            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }

            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }

            [JsonPropertyName("middle_name")]
            public string? MiddleName { get; set; }

            [JsonPropertyName("short_name")]
            public string? ShortName { get; set; }

            // Computed properties with your image detection logic
            [JsonIgnore]
            public string? ProfileAvatar => GetProfileAvatar();

            [JsonIgnore]
            public string? DisplayName => !string.IsNullOrEmpty(Username) ? $"{Username}#{Discriminator}" : Name;

            [JsonIgnore]
            public string? Provider { get; set; }

            [JsonIgnore]
            private bool IsDiscordAccount => !string.IsNullOrEmpty(ImageUrl) && ImageUrl.Contains("discord");

            private string GetProfileAvatar()
            {
                // Your logic translated to C#
                if (Picture is JsonElement pictureElement)
                {
                    // Check if picture is a nested object structure (Facebook)
                    if (pictureElement.ValueKind == JsonValueKind.Object &&
                        pictureElement.TryGetProperty("data", out JsonElement dataElement) &&
                        dataElement.ValueKind == JsonValueKind.Object &&
                        dataElement.TryGetProperty("url", out JsonElement urlElement) &&
                        urlElement.ValueKind == JsonValueKind.String)
                    {
                        return urlElement.GetString();
                    }
                }

                if (IsDiscordAccount)
                {
                    return ImageUrl;
                }

                // Handle simple string picture (Google) or fallback
                if (Picture is string pictureString)
                {
                    return pictureString;
                }

                // Fallback to Avatar for Discord or empty string
                return Avatar ?? string.Empty;
            }

            // Helper method to handle dynamic data (useful when deserializing from unknown sources)
            public static OAuthUserProfile FromDynamicObject(dynamic data)
            {
                var profile = new OAuthUserProfile
                {
                    Id = data.id?.ToString(),
                    Email = data.email?.ToString(),
                    Name = data.name?.ToString(),
                    Username = data.username?.ToString(),
                    Discriminator = data.discriminator?.ToString(),
                    Avatar = data.avatar?.ToString(),
                    ImageUrl = data.image_url?.ToString(),
                    GivenName = data.given_name?.ToString(),
                    FamilyName = data.family_name?.ToString(),
                    FirstName = data.first_name?.ToString(),
                    LastName = data.last_name?.ToString(),
                    Verified = data.verified as bool?,
                    VerifiedEmail = data.verified_email as bool?
                };

                // Handle the picture field with your specific logic
                bool isDiscordAccount = data.image_url != null && data.image_url.ToString().Contains("discord");

                if (data.picture != null)
                {
                    if (data.picture is Newtonsoft.Json.Linq.JObject pictureObj &&
                        pictureObj["data"] is Newtonsoft.Json.Linq.JObject dataObj)
                    {
                        // Facebook structure
                        profile.Picture = new FacebookPicture
                        {
                            Data = new FacebookPictureData
                            {
                                Url = dataObj["url"]?.ToString()
                            }
                        };
                    }
                    else
                    {
                        // Simple string or other type
                        profile.Picture = data.picture?.ToString();
                    }
                }

                // Determine provider based on available data
                if (isDiscordAccount || !string.IsNullOrEmpty(profile.Username))
                    profile.Provider = "Discord";
                else if (data.picture?.GetType() == typeof(Newtonsoft.Json.Linq.JObject))
                    profile.Provider = "Facebook";
                else if (!string.IsNullOrEmpty(profile.GivenName))
                    profile.Provider = "Google";

                return profile;
            }
        }

        // Supporting classes for Facebook picture structure
        public class FacebookPicture
        {
            [JsonPropertyName("data")]
            public FacebookPictureData Data { get; set; }
        }

        public class FacebookPictureData
        {
            [JsonPropertyName("height")]
            public int Height { get; set; }

            [JsonPropertyName("is_silhouette")]
            public bool IsSilhouette { get; set; }

            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("width")]
            public int Width { get; set; }
        }

        public static string GetEmailUsername(string email)
        {
            return email?.Split('@').FirstOrDefault() ?? string.Empty;
        }

        public static class CityBackgrounds
        {
            // Array of city image URLs from Unsplash
            public static readonly string[] CityImageUrls = new string[]
            {
                "https://images.unsplash.com/photo-1449824913935-59a10b8d2000?ixlib=rb-4.0.3&w=1200", // New York
                "https://images.unsplash.com/photo-1502602898536-47ad22581b52?ixlib=rb-4.0.3&w=1200", // Paris
                "https://images.unsplash.com/photo-1545048702-79362596cdc9?ixlib=rb-4.0.3&w=1200", // Tokyo
                "https://images.unsplash.com/photo-1512453979798-5ea266f8880c?ixlib=rb-4.0.3&w=1200", // Dubai
                "https://images.unsplash.com/photo-1523531294919-4bcd7c65e216?ixlib=rb-4.0.3&w=1200", // London
                "https://images.unsplash.com/photo-1587474260584-136574528ed5?ixlib=rb-4.0.3&w=1200", // Istanbul
                "https://images.unsplash.com/photo-1542662565-7e4b66bae529?ixlib=rb-4.0.3&w=1200", // Singapore
                "https://images.unsplash.com/photo-1505881502353-a1986add3762?ixlib=rb-4.0.3&w=1200", // Sydney
                "https://images.unsplash.com/photo-1564500604525-89cbc2baf5d8?ixlib=rb-4.0.3&w=1200", // Hong Kong
                "https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?ixlib=rb-4.0.3&w=1200"  // Chicago
            };
        }

        // Method to get a random city image URL from the array
        public static string GetRandomCityImage()
        {
            int index = new Random().Next(CityBackgrounds.CityImageUrls.Length);
            return CityBackgrounds.CityImageUrls[index];
        }


        public class SessionUser
        {
            public SessionUser(IDictionary<string, object> sessionData)
            {
                this.Id = sessionData.ContainsKey("id") ? sessionData["id"].ToString() : "";
                this.Username = sessionData.ContainsKey("username") ? sessionData["username"].ToString() : "";
                this.CountryOfOrigin = sessionData.ContainsKey("countryOfOrigin") ? sessionData["countryOfOrigin"].ToString() : "";
                this.FirstName = sessionData.ContainsKey("firstName") ? sessionData["firstName"].ToString() : "";
                this.LastName = sessionData.ContainsKey("lastName") ? sessionData["lastName"].ToString() : "";
                this.Bio = sessionData.ContainsKey("bio") ? sessionData["bio"].ToString() : "";
                this.Email = sessionData.ContainsKey("email") ? sessionData["email"].ToString() : "";
                this.Phone = sessionData.ContainsKey("phone") ? sessionData["phone"].ToString() : "";
                this.BgThumbnail = sessionData.ContainsKey("bgThumbnail") ? sessionData["bgThumbnail"].ToString() : "";
                this.Avatar = sessionData.ContainsKey("avatar") ? sessionData["avatar"].ToString() : "";

                // For properties that require casting, TryGetValue might be safer
                this.DateOfBirth = sessionData.TryGetValue("dateOfBirth", out dynamic dob) ? dob : null;
                this.GeoId = sessionData.ContainsKey("geoId") ? sessionData["geoId"].ToString() : "";
                this.MaritalStatus = sessionData.ContainsKey("maritalStatus") ? sessionData["maritalStatus"].ToString() : "";
                this.Religion = sessionData.ContainsKey("religion") ? sessionData["religion"].ToString() : "";
                this.PreferredMadhab = sessionData.ContainsKey("preferredMadhab") ? sessionData["preferredMadhab"].ToString() : "";

                this.Hobbies = sessionData.TryGetValue("hobbies", out dynamic hobbies) ? hobbies : new string[] { };
                this.FrequentMasjid = sessionData.ContainsKey("frequentMasjid") ? sessionData["frequentMasjid"].ToString() : "";
                this.FavoriteQuranReciters = sessionData.TryGetValue("favoriteQuranReciters", out dynamic reciters) ? reciters : new string[] { };
                this.FavoriteIslamicScholars = sessionData.TryGetValue("favoriteIslamicScholars", out dynamic scholars) ? scholars : new string[] { };
                this.IslamicStudyTopics = sessionData.TryGetValue("islamicStudyTopics", out dynamic topics) ? topics : new string[] { };

                this.Verified = sessionData.TryGetValue("verified", out object verified) ? (bool)verified : false;
                this.IsCompleted = sessionData.TryGetValue("isCompleted", out object completed) ? (bool)completed : false;
                this.CreatedAt = sessionData.TryGetValue("createdAt", out dynamic createdAt) ? createdAt : null;
            }
            public string? Id { get; set; }
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
            public string[] Bookmarks { get; set; }
            public string[] Reposts { get; set; }
            public string[] LikedPosts { get; set; }
        }
    }
     
}
