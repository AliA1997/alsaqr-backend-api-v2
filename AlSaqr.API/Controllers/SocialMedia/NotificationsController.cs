using Microsoft.AspNetCore.Mvc;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationsController : ControllerBase
    {

        private readonly ILogger<NotificationsController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly INotificationRepository _notificationRepository;

        public NotificationsController(ILogger<NotificationsController> logger, Supabase.Client supabase, INotificationRepository notificationRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _notificationRepository = notificationRepository;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetNotifications(
            Guid userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? all = "false")
        {
            if (userId == Guid.Empty)
                return BadRequest("You need to be logged in, in order to access your notifications.");

            var result = await _notificationRepository.GetNotifications(_supabase, userId, all, null, currentPage, itemsPerPage);

            _logger.LogInformation("Notifications retrieved successfully for user {UserId}", userId);

            return Ok(result);
        }

    }
}
