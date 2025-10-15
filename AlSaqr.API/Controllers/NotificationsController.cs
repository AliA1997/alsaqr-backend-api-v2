using AlSaqr.Domain.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using AlSaqr.Data;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationsController : ControllerBase
    {

        private readonly ILogger<NotificationsController> _logger;
        private readonly IDriver _driver;


        public NotificationsController(ILogger<NotificationsController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetNotifications(
            string userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? all = "false")
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("You need to be logged in, in order to access your notifications.");

            await using var session = _driver.AsyncSession();
            var notifications = new List<Dictionary<string, object>>();
            Pagination? pagination = null;
            var getAll = (all == "false");

            try
            {
                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                selectQuery = @"
                    MATCH (user:User {id: $userId})-[:NOTIFIED_BY]->(notification:Notification { read: $read })
                    WITH notification
                    RETURN notification                         
                ";

                selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "read", !getAll },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage }
                    },
                    new[] { "notification" }
                );

                pagingResult = await Neo4jHelpers.ReadAsync(
                    session,
                    Neo4jHelpers.CommonCountCipher(selectQuery, "notification"),
                    new Dictionary<string, object> 
                    {
                        { "userId", userId },
                        { "read", !getAll },
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

                notifications = selectResult ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Get notifications error!");
                return BadRequest("Get notifications error!");
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(notifications, pagination!));
        }

    }
}
