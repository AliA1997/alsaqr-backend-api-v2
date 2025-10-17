using AlSaqr.Data;
using AlSaqr.Domain.Common;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommentsController : ControllerBase
    {

        private readonly ILogger<CommentsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;


        public CommentsController(ILogger<CommentsController> logger, IDriver driver, IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Get a specific comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpGet("{commentId}")]
        public async Task<IActionResult> GetComment(string commentId)
        {
            // Input validation
            if (string.IsNullOrEmpty(commentId))
            {
                return BadRequest("Comment ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                string selectQuery = @"
                    MATCH (c: Comment { id: $commentId })
                    MATCH (u: User)-[:COMMENTED]->(c)
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
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        {"commentId", commentId }
                    },
                    new[] {
                        "id", "postId", "userId", "image", "text",
                        "createdAt", "username", "profileImg",
                        "commenters", "comments", "reposters", "likers"
                    }
                );

                // Get the first comment or null if no results
                var comment = selectResult?.FirstOrDefault();

                if (comment == null)
                {
                    return NotFound(new { message = $"Comment not found based on comment id {commentId}", success = false });
                }

                return Ok(new { comment, success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Comment error!");
                return StatusCode(500, new { message = "Fetch Comment error!", success = false });
            }
        }

        /// <summary>
        /// Create a comment
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateComment(
            [FromBody] AlSaqrUpsertRequest<Posts.CreateCommentDto> request,
            [FromQuery] bool onComment = false)
        {
            var data = request.Values;

            if (string.IsNullOrEmpty(data.UserId))
            {
                return BadRequest("User ID is required");
            }

            await using var session = _driver.AsyncSession();

            if (string.IsNullOrEmpty(data?.Text))
            {
                return BadRequest("Text of the Comment is required");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                if(loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a comment.");
                }

                var createCipher = "";
                if(onComment)
                {
                    createCipher = @"
                        MATCH (u:User {id: $userId}), (cmtCmt: Comment { id: $commentId })
                            CREATE (u)-[:COMMENTED_ON_COMMENT]->(cmt:Comment {
                              id: $id,
                              postId: $postId,
                              userId: $userId,
                              text: $text,
                              image: $image,
                              createdAt: datetime(),
                              updatedAt: null,
                              _rev: '',
                              _type: 'comment',
                            })
                            MERGE (cmt)-[:COMMENT_HAS_COMMENT]->(cmtCmt)
                    ";
                }
                else
                {
                    createCipher = @"
                        MATCH (u:User {id: $userId}), (post:Post { id: $postId })
                            CREATE (u)-[:COMMENTED]->(cmt:Comment {
                                id: $id,
                                postId: $postId,
                                userId: $userId,
                                text: $text,
                                image: $image,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: '',
                                _type: 'comment'
                            })
                            CREATE (cmt)-[:COMMENT_ON]->(post)
                    ";
                }

                await Neo4jHelpers.WriteAsync(
                    session,
                    createCipher,
                    new Dictionary<string, object>()
                    {
                        { "id", data.Id },
                        { "userId", loggedInUser.Id  ?? "" },
                        { "postId", data.PostId },
                        { "text", data.Text },
                        { "image", data.Image ?? "" }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match commenting user
                        MATCH (commentUser: User { id: $userId})
                        // Match the post node 
                        MATCH(post: Post { id: $postId})
                        // Match the post author (fixed relationship direction)
                        MATCH(author: User) - [:POSTED]->(post)
                        // Create notification connected to author
                        CREATE(author) - [:NOTIFIED_BY]->(n: Notification {
                            id: ""notification_"" + randomUUID(),
                            message: ""Post commented by "" + commentUser.username,
                            read: false,
                            relatedEntityId: post.id,
                            link: ""/status/"" + post.id,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""comment_on_post""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                      { "userId", loggedInUser.Id  ?? "" },
                      { "postId", data.PostId },
                    }
                 );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating post: {err.Message}");
                return StatusCode(500, new { message = "Add post error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Like a specific comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpPatch("{commentId}/liked")]
        public async Task<IActionResult> LikedComment(
            string commentId,
            [FromBody] AlSaqrUpsertRequest<LikeRequest> request)
        {
            var data = request.Values;
            if (string.IsNullOrEmpty(data.StatusId))
            {
                return BadRequest("Comment ID is required");
            }

            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("User must be logged in to like a comment");

            var userId = user.Id;

            await using var session = _driver.AsyncSession();

            try
            {
                if (data.Liked == false)
                {
                    // Add reposted relationship
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user node
                          MERGE (u:User {id: $userId})
                          // Match the comment node
                          MERGE (comment:Comment {id: $commentId})
                          // Create the 'LIKES_COMMENT' relationship with a timestamp
                          MERGE (u)-[ur:LIKES_COMMENT]->(comment)
                          MERGE (comment)-[commentRel:COMMENT_LIKED]->(u)
                          ON CREATE SET ur.timestamp = timestamp()
                          ON CREATE SET commentRel.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match liking user
                            MATCH (likingUser:User {id: $userId})
                            // Match the comment node
                            MATCH (comment:Comment {id: $commentId})
                            // Match the comment author (fixed relationship direction)
                            MATCH (author:User)-[:COMMENTED]->(comment)
                            // Create notification connected to author
                            CREATE (author)-[:NOTIFIED_BY]->(n:Notification {
                              id: ""notification_"" + randomUUID(),
                              message: ""Comment liked by "" + likingUser.username,
                              read: false,
                              relatedEntityId: comment.id,
                              link: ""/status/"" + comment.id,
                              createdAt: datetime(),
                              updatedAt: null,
                              _rev: null,
                              _type: ""notification"",
                              notificationType: ""liked_comment""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );
                }
                else if (data.Liked == true)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user, comment, and the 'LIKES_COMMENT' relationship
                          MATCH (u:User {id: $userId})-[r:LIKES_COMMENT]->(comment:Comment {id: $commentId })
                          MATCH (comment:Comment {id: $commentId})-[tr:COMMENT_LIKED]->(u:User {id: $userId})
                          DELETE r, tr
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match the liking user and comment
                            MATCH (likingUser:User {id: $userId})
                            MATCH (comment:Comment {id: $commentId})
                            // Match the author who created the comment
                            MATCH (author:User)-[:COMMENTED]->(comment)
                            // Find and delete the specific notification
                            MATCH (author)-[r:NOTIFIED_BY]->(n:Notification {
                              relatedEntityId: comment.id,
                              notificationType: ""liked_comment""
                            })
                            DELETE r, n
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Liked Comment error!");
                return StatusCode(500, new { message = "Liked Comment error!", success = false });
            }
        }

        /// <summary>
        /// Repost a specific comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpPatch("{commentId}/repost")]
        public async Task<IActionResult> RepostComment(
            string commentId,
            [FromBody] AlSaqrUpsertRequest<RePostRequest> request)
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(data.StatusId))
            {
                return BadRequest("Comment ID is required");
            }
            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("Must be logged in to repost a comment.");

            var userId = user.Id;

            await using var session = _driver.AsyncSession();

            try
            {
                if (data.Reposted == false)
                {
                    // Add reposted relationship
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                              // Match the user node
                              MERGE (u:User {id: $userId})
                              // Match the comment node
                              MERGE (c:Comment {id: $commentId})
                              // Create the 'REPOSTED_COMMENT' relationship with a timestamp
                              MERGE (u)-[ur:REPOSTED_COMMENT]->(c)
                              MERGE (c)-[tr:COMMENT_REPOSTS]->(u)
                              ON CREATE SET ur.timestamp = timestamp()
                              ON CREATE SET cr.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                              // Match reposting user
                              MATCH (repostingUser:User {id: $userId})
                              // Match the comment node
                              MATCH (comment:Comment {id: $commentId})
                              // Match the comment author (fixed relationship direction)
                              MATCH (author:User)-[:COMMENTED]->(comment)
                              // Create notification connected to author
                              CREATE (author)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: ""Comment reposted by "" + repostingUser.username,
                                read: false,
                                relatedEntityId: comment.id,
                                link: ""/status/"" + comment.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""reposted_comment""
                              })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );
                }
                else if (data.Reposted == true)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            MATCH (u:User {id: $userId})-[repostedRel:REPOSTED_COMMENT]->(c:Comment {id: $commentId})
                            MATCH (ct:Comment {id: $commentId})-[repostsRel:COMMENT_REPOSTS]->(u:User {id: $userId})
                            DELETE repostedRel, repostsRel
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the reposting user and comment
                          MATCH (repostingUser:User {id: $userId})
                          MATCH (comment:Comment {id: $commentId})
                          // Match the author who created the comment
                          MATCH (author:User)-[:COMMENTED]->(comment)
                          // Find and delete the specific notification
                          MATCH (author)-[r:NOTIFIED_BY]->(n:Notification {
                            relatedEntityId: comment.id,
                            notificationType: ""reposted_comment""
                          })
                          DELETE r, n
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "commentId", commentId }
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

        /// <summary>
        /// Delete a specific comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{commentId}")]
        public async Task<IActionResult> DeleteComment(
            string userId,
            string commentId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }
            // Input validation
            if (string.IsNullOrEmpty(commentId))
            {
                return BadRequest("Comment ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (cmt: Comment { id: $commentId })
                        WHERE cmt.userId = $userId
                        DETACH DELETE cmt;
                    ",
                    new Dictionary<string, object>() 
                    {
                        { "userId", userId },
                        { "commentId", commentId }
                    }
                );
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete comment error!");
                return StatusCode(500, new { message = "Delete comment error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
