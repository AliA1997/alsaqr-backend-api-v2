using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Infrastructure.SocialMediaCache;
using AlSaqr.Infrastructure;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : AuthorizedControllerBase
    {

        private readonly ILogger<MessagesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserCacheService _userCacheService;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public MessagesController(
            ILogger<MessagesController> logger, 
            Supabase.Client supabase,
            IMessageRepository messageRepository,
            IUserCacheService userCacheService,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _messageRepository = messageRepository;
            _userCacheService = userCacheService;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Send a message to a user.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("sendMessage")]
        public async Task<IActionResult> SendMessage(
                [FromBody] AlSaqrUpsertRequest<Messages.MessageFormDto> request
            )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            if (userId != data.SenderId)
                return BadRequest("Logged in user can only send this message.");
            if (data.RecipientId == Guid.Empty)
                return BadRequest("Receiver is required.");
            if (string.IsNullOrEmpty(data.Text))
                return BadRequest("Text of the message is required.");

            await _messageRepository.SendMessage(_supabase, userId, data);
            _logger.LogInformation("Message sent.");
            _socialMediaCacheService.ClearInitialMessageThreads(userId);

            return Ok(new { Success = true });
        }

    }
}
