using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;

using Neo4j.Driver;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {

        private readonly ILogger<UsersController> _logger;
        private readonly IDriver _driver;
        private readonly Supabase.Client _supabase;
        private readonly IUserRepository _userRepository;
        private readonly IUserFollowRepository _userFollowRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public UsersController(
            ILogger<UsersController> logger,
            IUserRepository userRepository,
            Supabase.Client supabase,
            IUserFollowRepository userFollowRepository,
            IMessageRepository messageRepository,
            INotificationRepository notificationRepository,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _userRepository = userRepository;
            _supabase = supabase;
            _userFollowRepository = userFollowRepository;
            _messageRepository = messageRepository;
            _notificationRepository = notificationRepository;
            _socialMediaCacheService = socialMediaCacheService;
        }


        /// <summary>
        /// Update user based on user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(
            Guid userId,
            [FromBody] User.UpdateUserDto data)
        {
            if (userId == Guid.Empty || string.IsNullOrEmpty(userId.ToString()))
            {
                return BadRequest("User ID is required for updating your user.");
            }
            
            try
            {
                await _userRepository.UpdateUser(_supabase, userId, data);

                return Ok(new { succcess = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error updating user: {err.Message}");
                return StatusCode(500, new { message = "Update user error!", success = false });
            }
        }

        /// <summary>
        /// Delete user based on user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            // Input validation
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

            try
            {
                await _userRepository.DeleteUser(_supabase, userId);
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete user error!");
                return StatusCode(500, new { message = "Delete user error!", success = false });
            }
        }


        /// <summary>
        /// Follow user endpoint
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/follow")]
        public async Task<IActionResult> Follow(
            Guid userId,
            [FromBody] User.FollowUserFormDto request)
        {
            // Input validation
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required for following someone.");
            }

            try
            {
                await _userFollowRepository.AddUserFollow(_supabase, userId, request);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Follow User error!");
                return StatusCode(500, new { message = "Follow User error!", success = false });
            }
        }

        /// <summary>
        /// UnFollow user endpoint
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/unfollow")]
        public async Task<IActionResult> UnFollow(
            Guid userId,
            [FromBody] User.UnFollowUserFormDto request)
        {
            // Input validation
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required for unfollowing someone.");
            }

            try
            {
                await _userFollowRepository.RemoveUserFollow(_supabase, userId, request);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "UnFollow User error!");
                return StatusCode(500, new { message = "UnFollow User error!", success = false });
            }

        }

        /// <summary>
        /// Get posts to add whenever creating entity items such as communities, and lists.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}/postsToAdd")]
        public async Task<IActionResult> GetPostsToAdd(
            Guid userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            if (userId == Guid.Empty)
                return BadRequest("User Id is required for getting posts to add.");

            var result = await _userRepository.GetPostsToAdd(_supabase, userId, searchTerm, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Get users to add whenever creating entity items such as communities, and lists.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}/usersToAdd")]
        public async Task<IActionResult> GetUsersToAdd(
                Guid userId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {
            if (userId == Guid.Empty)
                return BadRequest("User Id is required for getting users to add.");


            if (_socialMediaCacheService.CheckIfInitialUsersToAddCanBeRetrieved(currentPage, userId.ToString()))
                return Ok(_socialMediaCacheService.GetInitialUsersToAdd(userId.ToString()));

            var result = await _userRepository.GetUsersToAdd(_supabase, userId, searchTerm, currentPage, itemsPerPage);

            _socialMediaCacheService.SetUsersToAdd(result, userId.ToString());

            return Ok(result);

        }

        /// <summary>
        /// Get message history items or message thread for a loggedin user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("{userId}/messages")]
        public async Task<IActionResult> Messages(
            Guid userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            if (userId == Guid.Empty)
                return BadRequest("You need to be logged in, in order to access your direct messages.");

            var result = await _messageRepository.GetMessages(_supabase, userId, null, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Complete registration for a newly logged in user via social media provider.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CompleteRegistration(
                Guid userId,
                [FromBody] User.UserRegisterFormDto data)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required to complete registration");
            }

            try { 
                await _userRepository.CompleteRegistration(_supabase, userId, data);

                await _notificationRepository.CreateNotification(
                    _supabase,
                    userId: userId,
                    notificationMsg: "You Completed your account registration.",
                    notificationType: "your_account",
                    link: $"/users/{data.Username}",
                    entityType: "user",
                    relatedEntityId: userId
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error completing registration: {err.Message}");
                return StatusCode(500, new { message = " completing registration error!", success = false });
            }
        }

    }
}