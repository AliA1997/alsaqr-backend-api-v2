using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProfileController : ControllerBase
    {

        private readonly ILogger<ProfileController> _logger;
        private readonly IDriver _driver;


        public ProfileController(ILogger<ProfileController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        /// <summary>
        /// Get user profile information based on username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>

        [HttpGet("{username}")]
        public async Task<IActionResult> GetProfileInfo(
            string username)
        {
            // Input validation
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                string selectQuery = @"
                    MATCH (user:User {username: $username})
                    OPTIONAL MATCH (user)-[:BOOKMARKED]->(bookmark:Post)
                    OPTIONAL MATCH (user)-[:FOLLOW_USER]->(followedUser:User)
                    OPTIONAL MATCH (follower:User)-[fr:FOLLOW_USER]->(user)
                    RETURN user,
                      COLLECT(DISTINCT bookmark.id) AS bookmarks,
                      COLLECT(DISTINCT followedUser) AS following,
                      COLLECT(DISTINCT follower) AS followers
                ";


                var selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        { "username", username }
                    },
                    new[] {
                        "user", "bookmarks", "following", "followers"
                    }
                );

                var userProfileInfo = selectResult?.FirstOrDefault();

                if (userProfileInfo == null)
                {
                    return NotFound(new { message = $"User Profile with a username of {username}", success = false });
                }

                return Ok(userProfileInfo);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Profile error!");
                return StatusCode(500, new { message = "Fetch User Profile error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
