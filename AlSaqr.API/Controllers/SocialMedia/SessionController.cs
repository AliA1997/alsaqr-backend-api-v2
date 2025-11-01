using AlSaqr.Data.Helpers;
using AlSaqr.Domain.Common;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Runtime.ConstrainedExecution;
using static AlSaqr.Domain.Utils.Session;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class SessionController : ControllerBase
    {

        private readonly ILogger<SessionController> _logger;
        private readonly IDriver _driver;
        private readonly IConfiguration _configuration;
        private readonly IUserCacheService _userCacheService;

        public SessionController(
            ILogger<SessionController> logger, 
            IDriver driver, 
            IConfiguration configuration,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
            _configuration = configuration;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Signin or check neo4j data when signing in with supabase.
        /// </summary>
        /// <returns></returns>
        [HttpPost("signin")]
        public async Task<IActionResult> SignInWithSupabase([FromBody] Common.AlSaqrUpsertRequest<OAuthUserProfile> request)
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(data.Email))
            {
                return BadRequest("Enail is required");
            }
            var checkUserQuery = "MATCH(user: User { email: $email}) return user";

            await using var session = _driver.AsyncSession();

            try
            {
                var userResult = await Neo4jHelpers.ReadAsync(
                    session,
                    checkUserQuery,
                    new Dictionary<string, object>
                    {
                        { "email", data.Email }
                    },
                    new[] { "user" }
                );

            
                var user = userResult?.FirstOrDefault();

                if (user == null && !string.IsNullOrEmpty(data.Email))
                {
                    string mutateCipher = @"
                      CREATE (u:User {
                        id: $id,
                        createdAt: $createdAt,
                        updatedAt: $updatedAt,
                        username: $username,
                        countryOfOrigin: $countryOfOrigin,
                        email: $email,
                        firstName: $firstName,
                        lastName: $lastName,
                        phone: $phone,
                        bio: $bio,
                        bgThumbnail: $bgThumbnail,
                        avatar: $avatar,
                        dateOfBirth: $dateOfBirth,
                        geoId: $geoId,
                        maritalStatus: $maritalStatus,
                        religion: $religion,
                        preferredMadhab: $preferredMadhab,
                        hobbies: $hobbies,
                        frequentMasjid: $frequentMasjid,
                        favoriteQuranReciters: $favoriteQuranReciters,
                        favoriteIslamicScholars: $favoriteIslamicScholars,
                        islamicStudyTopics: $islamicStudyTopics,
                        verified: false,
                        isCompleted: false
                      })
                    ";
                    var isDiscordAccount = !string.IsNullOrEmpty(data.ImageUrl) ? data.ImageUrl.Contains("discord") : false;

                    await Neo4jHelpers.WriteAsync(
                        session,
                        mutateCipher,
                        new Dictionary<string, object>
                        {
                            { "id", Guid.NewGuid() },
                            { "createdAt", new DateTime().ToString() },
                            { "updatedAt", null },
                            { "username", isDiscordAccount ? data.GlobalName : GetEmailUsername(data.Email ?? "") },
                            { "email", data.Email },
                            { "firstName", string.IsNullOrEmpty(data?.FirstName) ? data.Name : data?.FirstName ?? "" },
                            { "lastName", string.IsNullOrEmpty(data?.LastName) ? data.Name : data?.LastName ?? "" },
                            { "bio", "" },
                            { "countryOfOrigin", "United States" },
                            { "phone", null },
                            { "avatar", data.Picture != null ? data?.Picture : data?.ImageUrl },
                            { "bgThumbnail", GetRandomCityImage() },
                            { "dateOfBirth", null },
                            { "geoId", null },
                            { "maritalStatus", "Single" },
                            { "preferredMadhab", "Hanafi" },
                            { "religion", "Muslim" },
                            { "hobbies", new List<object>() },
                            { "frequentMasjid", "" },
                            { "favoriteQuranReciters", new List<object>() },
                            { "favoriteIslamicScholars", new List<object>() },
                            { "islamicStudyTopics", new List<object>() }
                        }
                    );
                    
                }

                _logger.LogInformation("User signed in successfully!");
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Post error!");
                return StatusCode(500, new { message = "Fetch Post error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }


        /// <summary>
        /// Check user if he's logged in.
        /// </summary>
        /// <returns></returns>
        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] Common.AlSaqrUpsertRequest<SessionCheckRequest> request)
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(data.Email))
            {
                return BadRequest("Enail is required");
            }
            var sessionQuery = @"
                MATCH (user:User {email: $email})
                OPTIONAL MATCH (user)-[:BOOKMARKED]->(bookmark:Post)
                OPTIONAL MATCH (user)-[:REPOSTED]->(repost:Post)
                OPTIONAL MATCH (user)-[:LIKES]->(likedPost:Post)
                OPTIONAL MATCH (user)-[:COMMENTED]->(repliedPost: Post)
                RETURN user,
                    COLLECT(bookmark) AS bookmarks,
                    COLLECT(repost) AS reposts,
                    COLLECT(likedPost) AS likedPosts,
                    COLLECT(repliedPost) as repliedPosts
            ";

            await using var session = _driver.AsyncSession();

            try
            {
                var userResult = await Neo4jHelpers.ReadUserData(
                    session,
                    sessionQuery,
                    new Dictionary<string, object>
                    {
                        { "email", data.Email }
                    },
                    new[] { "user", "bookmarks", "reposts", "likedPosts" }
                );


                var userRes = userResult?.FirstOrDefault();

                if (userRes == null || userRes.User == null)
                    return BadRequest($"User not found for {data.Email}");

                _logger.LogInformation("User signed in successfully!");
                var user = userRes;
                var bookmarkIds = user.Bookmarks != null
                                    ? user.Bookmarks.Select(x => x["id"].ToString() ?? "").ToList()
                                    : new List<string>();

                var repostsIds = user.Reposts != null
                    ? user.Reposts.Select(x => x["id"].ToString() ?? "").ToList()
                    : new List<string>();
                var likedPostsIds = user.LikedPosts != null
                    ? user.LikedPosts.Select(x => x["id"].ToString() ?? "").ToList()
                    : new List<string>();


                var sessionUser = new SessionUser(user.User!)
                {
                    Bookmarks = bookmarkIds.ToArray(),
                    Reposts = repostsIds.ToArray(),
                    LikedPosts = likedPostsIds.ToArray()
                };
                var userId = user.User["id"].ToString();

                if (string.IsNullOrEmpty(userId))
                    return BadRequest("Invalid user retrieved");

                _userCacheService.SetLoggedInUser(sessionUser);

                return Ok(new {  result = sessionUser });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Session error!");
                return StatusCode(500, new { message = "Fetch User Session error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

    }
}
