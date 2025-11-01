using AlSaqr.Domain.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using static AlSaqr.Domain.Utils.User;
using AlSaqr.Data.Helpers;

namespace AlSaqr.API.Controllers.SocialMedia
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


        /// <summary>
        /// Get user profile posts based on username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>

        [HttpGet("{username}/posts")]
        public async Task<IActionResult> GetProfilePosts(
            string username,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 50)
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
                    // User Tweets
                    MATCH (u:User {username: $username})
                    MATCH (post:Post {userId: u.id})
                    OPTIONAL MATCH (post)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post, u.username as username, u.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers, ""user"" as type
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers, type
                    SKIP $skip
                    LIMIT $itemsPerPage

                    UNION

                    // Bookmarked Tweets
                    MATCH (u:User {username: $username})
                    MATCH (post:Post), (postUser: User { id: post.userId })
                    WHERE (u)-[:BOOKMARKED]->(post)
                    OPTIONAL MATCH (post)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post, postUser.username as username, postUser.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers, ""bookmarked"" as type
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers, type
                    SKIP $skip
                    LIMIT $itemsPerPage

                    UNION

                    // Liked Tweets
                    MATCH (u:User {username: $username})
                    MATCH (post:Post), (postUser: User { id: post.userId })
                    WHERE (u)-[:LIKES]->(post)
                    OPTIONAL MATCH (post)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post, postUser.username as username, postUser.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers, ""liked"" as type
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers, type
                    SKIP $skip
                    LIMIT $itemsPerPage

                    UNION

                    // Reposted Posts
                    MATCH (u:User {username: $username})
                    MATCH (post:Post), (postUser: User { id: post.userId })
                    WHERE (u)-[:REPOSTED]->(post)
                    OPTIONAL MATCH (post)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post, postUser.username as username, postUser.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers, ""reposted"" as type
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers, type
                    SKIP $skip
                    LIMIT $itemsPerPage

                    UNION

                    // Replied Tweets
                    MATCH (u:User {username: $username})
                    MATCH (post:Post), (comment:Comment), (postUser: User { id: post.userId })
                    WHERE (u)-[:COMMENTED]->(comment)-[:COMMENT_ON]->(post)
                    OPTIONAL MATCH (post)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post, postUser.username as username, postUser.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers, ""replied"" as type
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers, type
                    SKIP $skip
                    LIMIT $itemsPerPage
                ";


                var selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        { "username", username },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage },

                    },
                    new[] {
                        "post", "username", "profileImg", "comments", "commenters", "reposters", "likers", "type"
                    }
                );

                var bookmarkedPosts = selectResult.Where(n => n["type"].ToString() == "bookmarked").ToList();
                var likedPosts = selectResult.Where(n => n["type"].ToString() == "liked").ToList();
                var repostedPosts = selectResult.Where(n => n["type"].ToString() == "reposted").ToList();
                var userPosts = selectResult.Where(n => n["type"].ToString() == "user").ToList();
                var repliedPosts = selectResult.Where(n => n["type"].ToString() == "replied").ToList();

                return Ok(new UserProfilePostsResponse()
                {
                    UserPosts = userPosts,
                    BookmarkedPosts = bookmarkedPosts,
                    LikedPosts = likedPosts,
                    RepostedPosts = repostedPosts,
                    RepliedPosts = repliedPosts,
                    Success = true
                });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Profile Posts error!");
                return StatusCode(500, new { message = "Fetch User Profile Posts error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
