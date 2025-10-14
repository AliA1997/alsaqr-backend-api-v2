using AlSaqr.API.Utils;
using AlSaqr.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PostsController : ControllerBase
    {

        private readonly ILogger<PostsController> _logger;
        private readonly IDriver _driver;


        public PostsController(ILogger<PostsController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        [HttpGet]
        public async Task<IActionResult> GetPosts(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            await using var session = _driver.AsyncSession();
            var posts = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    selectQuery = @"
                    MATCH (post:Post), (user: User { id: post.userId })
                    WHERE post.text CONTAINS $searchTerm
                    OPTIONAL MATCH (post)<-[:COMMENT_ON]-(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:REPOSTS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post,
                        user.username as username,
                        user.avatar as profileImg,
                        COLLECT(DISTINCT c) AS comments,
                        COLLECT(DISTINCT u) AS commenters,
                        COLLECT(DISTINCT reposter) AS reposters,
                        COLLECT(DISTINCT liker) AS likers
                    ORDER BY post.createdAt DESC
                    RETURN post, username, profileImg, comments, commenters, reposters, likers";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "post", "username", "profileImg", "comments", "commenters", "reposters", "likers" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                        new Dictionary<string, object>
                        {
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                    MATCH (post:Post), (user: User { id: post.userId })
                    OPTIONAL MATCH (post)<-[:COMMENT_ON]-(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)-[:REPOSTS]->(reposter:User)
                    OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                    WITH post,
                        user.username as username,
                        user.avatar as profileImg,
                        COLLECT(DISTINCT c) AS comments,
                        COLLECT(DISTINCT u) AS commenters,
                        COLLECT(DISTINCT reposter) AS reposters,
                        COLLECT(DISTINCT liker) AS likers
                    ORDER BY post.createdAt DESC
                    RETURN post, username, profileImg, comments, commenters, reposters, likers";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "post", "username", "profileImg", "comments", "commenters", "reposters", "likers" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                        new Dictionary<string, object> {},
                        new[] { "total" }
                    );
                }

                int totalItems = pagingResult?.FirstOrDefault()?["total"] is long total ? (int)total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };

                posts = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(posts, pagination!));
        }

        [HttpGet("{postId}")]
        public async Task<IActionResult> GetPost(string postId)
        {
            // Input validation
            if (string.IsNullOrEmpty(postId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                string selectQuery = @"
                    MATCH (post:Post { id: $postId }), (user: User { id: post.userId })
                    OPTIONAL MATCH (post)<-[:COMMENT_ON]-(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (post)<-[:REPOSTED]-(reposter:User)
                    OPTIONAL MATCH (post)<-[:LIKES]-(liker:User)
                    WITH post,
                        user.username as username,
                        user.avatar as profileImg,
                        COLLECT(DISTINCT c) AS comments,
                        COLLECT(DISTINCT u) AS commenters,
                        COLLECT(DISTINCT reposter) AS reposters,
                        COLLECT(DISTINCT liker) AS likers
                    RETURN post,
                          username,
                          profileImg,
                          comments,
                          commenters,
                          reposters,
                          likers
                    LIMIT 100";

                var selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        {"postId", postId }
                    },
                    new[] {
                        "post", "username", "profileImg", "comments", "commenters", "reposters", "likers"
                    }
                );

                // Get the first comment or null if no results
                var post = selectResult?.FirstOrDefault();

                if (post == null)
                {
                    return NotFound(new { message = $"Post not found based on comment id {postId}", success = false });
                }

                return Ok(new { post, success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Post error!");
                return StatusCode(500, new { message = "Fetch Post error!", success = false });
            }
        }

        [HttpGet("{postId}/comments")]
        public async Task<IActionResult> GetPostComments(string postId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10)
        {
            // Input validation
            if (string.IsNullOrEmpty(postId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                string pagingQuery = @"SKIP $skip LIMIT $itemsPerPage";

                string selectQuery = @"
                    MATCH (c:Comment)-[:COMMENT_ON]->(post:Post {id: $postId})
                    MATCH (u:User)-[:COMMENTED]->(c)
                    OPTIONAL MATCH (c)-[:COMMENT_HAS_COMMENT]->(cmtCmt:Comment)<-[:COMMENTED_ON_COMMENT]-(cmtCmtUser:User)
                    OPTIONAL MATCH (c)-[:COMMENT_REPOSTS]->(reposter:User)
                    OPTIONAL MATCH (c)-[:COMMENT_LIKED]->(liker:User)
                    WITH u.id as userId, 
                        u.username as username,
                        u.avatar as profileImg,
                        COLLECT(DISTINCT cmtCmtUser) AS commenters,
                        COLLECT(DISTINCT cmtCmt) AS comments,
                        COLLECT(DISTINCT reposter) AS reposters,
                        COLLECT(DISTINCT liker) AS likers,
                        c
                    RETURN c.id as id,
                            c.postId as postId,
                            userId as userId,
                            c.image as image,
                            c.text as text,
                            c.createdAt as createdAt,
                            username,
                            profileImg,
                            commenters,
                            comments,
                            reposters,
                            likers";

                var selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>()
                    {
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage },
                        {"postId", postId },
                    },
                    new[] {
                        "id", "postId", "userId", "image", "text",
                        "createdAt", "username", "profileImg",
                        "commenters", "comments", "reposters", "likers"
                    }
                );

                var pagingResult = await Neo4jHelpers.ReadAsync(
                                        session,
                                        Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                                        new Dictionary<string, object> 
                                        {
                                            {"postId", postId },
                                        },
                                        new[] { "total" }
                                    );

                int totalItems = pagingResult?.FirstOrDefault()?["total"] is long total ? (int)total : 0;

                var pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };

                var comments = selectResult ?? new List<Dictionary<string, object>>();

                // Get the first comment or null if no results
                var post = selectResult?.FirstOrDefault();

                if (comments == null)
                {
                    return NotFound(new { message = $"Comments not found for the post id {postId}", success = false });
                }

                return Ok(new { comments, success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Post error!");
                return StatusCode(500, new { message = "Fetch Post error!", success = false });
            }
        }


        [HttpPatch("{postId}/bookmark")]
        public async Task<IActionResult> BookmarkPost(
            string postId,
            [FromBody] Posts.BookmarkRequest request)
        {
            var userId = "";
            // Input validation
            if (string.IsNullOrEmpty(request.StatusId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                if (request.Bookmarked == false)
                {
                    // Add bookmarked relationship
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match the user node
                            MERGE (u:User {id: $userId})
                            // Match the tweet node
                            MERGE (t:Post {id: $postId})
                            // Create the 'BOOKMARKED' relationship with a timestamp
                            MERGE (u)-[r:BOOKMARKED]->(t)
                            ON CREATE SET r.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                              // Match bookmarking user
                              MATCH (bookmarkingUser:User {id: $userId})
                              // Match the post node (fixed variable name from pst to post for consistency)
                              MATCH (post:Post {id: $postId})
                              // Match the post author (fixed relationship direction)
                              MATCH (author:User)-[:POSTED]->(post)
                              // Create notification connected to author
                              CREATE (author)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: ""Post bookmarked by "" + bookmarkingUser.username,
                                read: false,
                                relatedEntityId: post.id,
                                link: ""/status/"" + post.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""bookmarked_post""
                              })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }
                else if (request.Bookmarked == true)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            MATCH (u:User {id: $userId})-[r:BOOKMARKED]->(t:Post {id: $postId})
                            DELETE r
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the bookmarking user and post
                          MATCH (bookmarkingUser:User {id: $userId})
                          MATCH (post:Post {id: $postId})
                          // Match the author who created the post
                          MATCH (author:User)-[:POSTED]->(post)
                          // Find and delete the specific notification
                          MATCH (author)-[r:NOTIFIED_BY]->(n:Notification {
                            relatedEntityId: post.id,
                            notificationType: ""bookmarked_post""
                          })
                          DELETE r, n
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Liked Post error!");
                return StatusCode(500, new { message = "Liked Post error!", success = false });
            }

        }


        [HttpPatch("{postId}/liked")]
        public async Task<IActionResult> LikedPost(
            string postId, 
            [FromBody] Posts.LikePostRequest request)
        {
            var userId = "";
            // Input validation
            if (string.IsNullOrEmpty(request.StatusId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                if (request.Liked == false)
                {
                    // Add reposted relationship
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user node
                          MERGE (u:User {id: $userId})
                          // Match the post node
                          MERGE (post:Post {id: $postId})
                          // Create the 'LIKES' relationship with a timestamp
                          MERGE (u)-[ur:LIKES]->(post)
                          MERGE (post)-[postRel:LIKED]->(u)
                          ON CREATE SET ur.timestamp = timestamp()
                          ON CREATE SET postRel.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match liking user
                            MATCH (likingUser:User {id: $userId})
                            // Match the post node (fixed variable name from pst to post for consistency)
                            MATCH (post:Post {id: $postId})
                            // Match the post author (fixed relationship direction)
                            MATCH (author:User)-[:POSTED]->(post)
                            // Create notification connected to author
                            CREATE (author)-[:NOTIFIED_BY]->(n:Notification {
                              id: ""notification_"" + randomUUID(),
                              message: ""Post liked by "" + likingUser.username,
                              read: false,
                              relatedEntityId: post.id,
                              link: ""/status/"" + post.id,
                              createdAt: datetime(),
                              updatedAt: null,
                              _rev: null,
                              _type: ""notification"",
                              notificationType: ""liked_post""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }
                else if (request.Liked == true)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user, tweet, and the 'LIKES' relationship
                          MATCH (u:User {id: $userId})-[r:LIKES]->(post:Post {id: $postId})
                          MATCH (post:Post {id: $postId})-[tr:LIKED]->(u:User {id: $userId})
                          DELETE r, tr
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match the liking user and post
                            MATCH (likingUser:User {id: $userId})
                            MATCH (post:Post {id: $postId})
                            // Match the author who created the post
                            MATCH (author:User)-[:POSTED]->(post)
                            // Find and delete the specific notification
                            MATCH (author)-[r:NOTIFIED_BY]->(n:Notification {
                              relatedEntityId: post.id,
                              notificationType: ""liked_post""
                            })
                            DELETE r, n
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Liked Post error!");
                return StatusCode(500, new { message = "Liked Post error!", success = false });
            }

        }

        [HttpPatch("{postId}/repost")]
        public async Task<IActionResult> RePostPost(
            string postId, 
            [FromBody] Posts.RePostRequest request)
        {
            var userId = "";
            // Input validation
            if (string.IsNullOrEmpty(request.StatusId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                if (request.Reposted == false)
                {
                    // Add reposted relationship
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                              // Match the user node
                              MERGE (u:User {id: $userId})
                              // Match the tweet node
                              MERGE (t:Post {id: $postId})
                              // Create the 'BOOKMARKED' relationship with a timestamp
                              MERGE (u)-[ur:REPOSTED]->(t)
                              MERGE (t)-[tr:REPOSTS]->(u)
                              ON CREATE SET ur.timestamp = timestamp()
                              ON CREATE SET tr.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                              // Match reposting user
                              MATCH (repostingUser:User {id: $userId})
                              // Match the post node (fixed variable name from pst to post for consistency)
                              MATCH (post:Post {id: $postId})
                              // Match the post author (fixed relationship direction)
                              MATCH (author:User)-[:POSTED]->(post)
                              // Create notification connected to author
                              CREATE (author)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: ""Post reposted by "" + repostingUser.username,
                                read: false,
                                relatedEntityId: post.id,
                                link: ""/status/"" + post.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""reposted_post""
                              })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }
                else if (request.Reposted == true)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            MATCH (u:User {id: $userId})-[repostedRel:REPOSTED]->(t:Post {id: $postId})
                            MATCH (u:Post {id: $postId})-[retweetsRel:REPOSTS]->(u:User {id: $userId})
                            DELETE repostedRel, retweetsRel
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the reposting user and post
                          MATCH (repostingUser:User {id: $userId})
                          MATCH (post:Post {id: $postId})
                          // Match the author who created the post
                          MATCH (author:User)-[:POSTED]->(post)
                          // Find and delete the specific notification
                          MATCH (author)-[r:NOTIFIED_BY]->(n:Notification {
                            relatedEntityId: post.id,
                            notificationType: ""reposted_post""
                          })
                          DELETE r, n
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "postId", postId }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Repost error!");
                return StatusCode(500, new { message = "Repost error!", success = false });
            }
        }

        [HttpDelete("{postId}")]
        public async Task<IActionResult> DeletePost(string postId)
        {
            var userId = "61223c34-590a-4d59-8049-d828957d4a03";
            // Input validation
            if (string.IsNullOrEmpty(postId))
            {
                return BadRequest("Post ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          MATCH (pst: Post { id: $postId })
                          WHERE pst.userId = $userId
                          DETACH DELETE pst;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "postId", postId }
                    }
                );
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete post error!");
                return StatusCode(500, new { message = "Delete post error!", success = false });
            }
        }

    }
}
