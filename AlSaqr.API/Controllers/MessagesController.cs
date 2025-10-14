using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using System.Collections.Generic;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {

        private readonly ILogger<MessagesController> _logger;
        private readonly IDriver _driver;
        private readonly IConfiguration _configuration;


        public MessagesController(ILogger<MessagesController> logger, IDriver driver, IConfiguration configuration)
        {
            _logger = logger;
            _driver = driver;
            _configuration = configuration;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMessageThreads(
                string userId,
                [FromQuery] string receiverId,
                [FromQuery] string senderId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10
            )
        {
            await using var session = _driver.AsyncSession();
            var messageThreads = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (string.IsNullOrEmpty(userId))
                    return BadRequest("You need to be logged in, in order to access your direct messages.");

                selectQuery = @"
                  MATCH (message:Message { senderId: $senderId, recipientId: $recipientId})
                    RETURN message

                  UNION

                  MATCH (message:Message { senderId: $recipientId, recipientId: $senderId})
                    RETURN message
                ";

                selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>
                    {
                        { "userId", userId },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage },
                        { "senderId", senderId },
                        { "recipientId", receiverId }
                    },
                    new[] { "message" }
                );

                pagingResult = await Neo4jHelpers.ReadAsync(
                    session,
                    Neo4jHelpers.CommonCountCipher(selectQuery, "message"),
                    new Dictionary<string, object>
                    {
                        { "senderId", senderId },
                        { "recipientId", receiverId }
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

                messageThreads = selectResult ?? new List<Dictionary<string, object>>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting message threads for this list.");
                return BadRequest("Error getting message threads for this list.");
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(messageThreads, pagination!));
        }

        [HttpPost("{userId}/sendMessage")]
        public async Task<IActionResult> SendMessage(
                string userId,
                [FromBody] Messages.MessageFormDto data
            )
        {
            if (userId != data.SenderId)
                return BadRequest("Logged in user can only send this message.");

            if (string.IsNullOrEmpty(data.RecipientId))
                return BadRequest("Receiver is required.");

            if (string.IsNullOrEmpty(data.Text))
                return BadRequest("Text of the message is required.");

            await using var session = _driver.AsyncSession();

            try
            {
                var messageId = $"message_{Guid.NewGuid()}";

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Create a new message records, reference recipient information.
                        MERGE (sender:User {id: $senderId})
                        MERGE (receiver:User {id: $recipientId})
                        CREATE (sender)-[:SEND_MESSAGE]->(m:Message {
                          id: $id,
                          createdAt: datetime(),
                          updatedAt: null,  
                          senderId: $senderId,
                          senderUsername: $senderUsername,
                          senderProfileImg: $senderProfileImg,
                          recipientId: $recipientId,
                          recipientUsername: $recipientUsername,
                          recipientProfileImg: $recipientProfileImg,
                          text: $text,
                          image: $image,
                          messageType: $messageType
                        })
                        CREATE (receiver)-[:RECEIVED_MESSAGE]->(m)        
                    ",
                    new Dictionary<string, object>()
                    {
                        { "id", messageId },
                        { "senderId", data.SenderId },
                        { "senderUsername", data.SenderUsername },
                        { "senderProfileImg", data.SenderProfileImg },
                        { "recipientId", data.RecipientId },
                        { "recipientUsername", data.RecipientUsername },
                        { "recipientProfileImg", data.RecipientProfileImg },
                        { "text", data.Text },
                        { "image", data.Image },
                        { "messageType", data.MessageType },
                    }
                );

            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting message threads for this list.");
                return BadRequest("Error getting message threads for this list.");
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new { Success = true });
        }

    }
}
