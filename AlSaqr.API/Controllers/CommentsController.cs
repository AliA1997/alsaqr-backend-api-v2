using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommentsController : ControllerBase
    {

        private readonly ILogger<CommentsController> _logger;
        private readonly IDriver _driver;


        public CommentsController(ILogger<CommentsController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

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


        [HttpPatch("{commentId}/liked")]
        public async Task<IActionResult> LikedComment(string commentId)
        {
            return Ok();
        }

        [HttpPatch("{commentId}/repost")]
        public async Task<IActionResult> RepostComment(string commentId)
        {
            return Ok();
        }

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
