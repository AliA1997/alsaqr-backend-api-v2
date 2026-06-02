using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : ControllerBase
    {

        private readonly ILogger<MessagesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IMessageRepository _messageRepository;


        public MessagesController(
            ILogger<MessagesController> logger, 
            Supabase.Client supabase,
            IMessageRepository messageRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _messageRepository = messageRepository;
        }
        /// <summary>
        /// Get message threads for a given user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="receiverId"></param>
        /// <param name="senderId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetMessageThreads(
                Guid userId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10
            )
        {
            if(userId == Guid.Empty)
                return BadRequest("User ID is required.");

            var result = await _messageRepository.GetMessages(_supabase, userId, null, currentPage, itemsPerPage);
            
            return Ok(result);
        }

        /// <summary>
        /// Send a message to a user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/sendMessage")]
        public async Task<IActionResult> SendMessage(
                Guid userId,
                [FromBody] AlSaqrUpsertRequest<Messages.MessageFormDto> request
            )
        {
            var data = request.Values;

            if (userId != data.SenderId)
                return BadRequest("Logged in user can only send this message.");

            if (data.RecipientId == Guid.Empty)
                return BadRequest("Receiver is required.");

            if (string.IsNullOrEmpty(data.Text))
                return BadRequest("Text of the message is required.");

            await _messageRepository.SendMessage(_supabase, userId, data);

            _logger.LogInformation("Message sent.");

            return Ok(new { Success = true });
        }

    }
}
