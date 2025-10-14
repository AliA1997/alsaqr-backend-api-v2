using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using System.Collections.Generic;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BookmarksController : ControllerBase
    {

        private readonly ILogger<BookmarksController> _logger;
        private readonly IDriver _driver;


        public BookmarksController(ILogger<BookmarksController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetPosts(
                string userId,
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

                selectQuery = @"
                    MATCH (user:User {id: $userId})-[:BOOKMARKED]->(pst:Post)
                    OPTIONAL MATCH (postUser: User { id: pst.userId })
                    OPTIONAL MATCH (pst)-[:COMMENT_ON]->(c:Comment)<-[:COMMENTED]-(u:User)
                    OPTIONAL MATCH (pst)-[:REPOSTED]->(reposter:User)
                    OPTIONAL MATCH (pst)-[:LIKED]->(liker:User)
                    WITH pst as post, postUser.username as username, postUser.avatar as profileImg, COLLECT(DISTINCT c) AS comments, COLLECT(DISTINCT u) AS commenters, COLLECT(DISTINCT reposter) AS reposters, COLLECT(DISTINCT liker) AS likers
                    ORDER BY post.createdAt DESCENDING
                    RETURN post, username, profileImg, comments, commenters, reposters, likers";

                selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage }
                    },
                    new[] { "post", "username", "profileImg", "comments", "commenters", "reposters", "likers" }
                );

                pagingResult = await Neo4jHelpers.ReadAsync(
                    session,
                    Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                    },
                    new[] { "total" }
                );


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

    }
}
