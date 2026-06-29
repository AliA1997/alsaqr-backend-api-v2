using AlSaqr.Data.Repositories.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;


namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : AuthorizedControllerBase
    {

        private readonly ILogger<UsersController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IUserRepository _userRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IUserFollowRepository _userFollowRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;
        private readonly IUserCacheService _userCacheService;

        public UsersController(
            ILogger<UsersController> logger,
            IUserRepository userRepository,
            IProfileRepository profileRepository,
            Supabase.Client supabase,
            IUserFollowRepository userFollowRepository,
            IMessageRepository messageRepository,
            INotificationRepository notificationRepository,
            ISocialMediaCacheService socialMediaCacheService,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _userRepository = userRepository;
            _profileRepository = profileRepository;
            _supabase = supabase;
            _userFollowRepository = userFollowRepository;
            _messageRepository = messageRepository;
            _notificationRepository = notificationRepository;
            _socialMediaCacheService = socialMediaCacheService;
            _userCacheService = userCacheService;
        }


        /// <summary>
        /// Update user based on user id.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>

        [HttpPut]
        public async Task<IActionResult> UpdateUser([FromBody] AlSaqrUpsertRequest<User.UpdateUserDto> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;
            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);
            try
            {
                await _userRepository.UpdateUser(_supabase, userId, data, ct);
                var sessionUserResult = await _profileRepository.GetSessionInfo(_supabase, userId);
                _userCacheService.SetLoggedInUser(sessionUserResult);

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
        /// Delete user based on logged in user.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IActionResult> DeleteUser()
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

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
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("follow")]
        public async Task<IActionResult> Follow([FromBody] AlSaqrUpsertRequest<User.FollowUserFormDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            // Input validation
            if (userId == Guid.Empty)
                return BadRequest("User ID is required for following someone.");

            try
            {
                await _userFollowRepository.AddUserFollow(_supabase, userId, data, ct);

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
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("unfollow")]
        public async Task<IActionResult> UnFollow([FromBody] AlSaqrUpsertRequest<User.UnFollowUserFormDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);
            // Input validation
            if (userId == Guid.Empty)
                return BadRequest("User ID is required for unfollowing someone.");

            try
            {
                await _userFollowRepository.RemoveUserFollow(_supabase, userId, data, ct);

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
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("postsToAdd")]
        public async Task<IActionResult> GetPostsToAdd(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if(loggedInUser == null)
                return Unauthorized("To get posts to add when upserting a record, you need to be logged in.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            if(_socialMediaCacheService.CheckIfInitialPostsToAddCanBeRetrieved(userId))
                return Ok(_socialMediaCacheService.GetInitialPostsToAdd(userId));
     
            var result = await _userRepository.GetPostsToAdd(_supabase, userId, searchTerm, currentPage, itemsPerPage);

            _socialMediaCacheService.SetInitialPostsToAdd(userId, result);

            return Ok(result);
        }

        /// <summary>
        /// Get users to add whenever creating entity items such as communities, and lists.
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("usersToAdd")]
        public async Task<IActionResult> GetUsersToAdd(
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("To get users to add when upserting a record, you need to be logged in.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            if (string.IsNullOrEmpty(searchTerm?.Trim()) && _socialMediaCacheService.CheckIfInitialUsersToAddCanBeRetrieved(currentPage, userId))
                return Ok(_socialMediaCacheService.GetInitialUsersToAdd(userId));

            var result = await _userRepository.GetUsersToAdd(_supabase, userId, searchTerm, currentPage, itemsPerPage);

            _socialMediaCacheService.SetUsersToAdd(result, userId);

            return Ok(result);

        }

        /// <summary>
        /// Get message history items or message thread for a loggedin user.
        /// </summary>
        /// <param name="receiverId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("messages")]
        public async Task<IActionResult> Messages(
            [FromQuery] Guid receiverId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null)
                return Unauthorized("Need to be logged in to view your messages");

            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            if (receiverId == Guid.Empty)
                return BadRequest("You need a receiver in order to retrieve messages");

            var result = await _messageRepository.GetMessages(_supabase, userId, receiverId, null, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Get Message threads for a logged in user.
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("messageThreads")]
        public async Task<IActionResult> MessageThreads(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null)
                return Unauthorized("Need to be logged in to view your messages");

            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            var result = await _messageRepository.GetMessageThreads(_supabase, userId, null, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Complete registration for a newly logged in user via social media provider.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CompleteRegistration(
                Guid userId,
                [FromBody] AlSaqrUpsertRequest<User.UserRegisterFormDto> request)
        {
            var data = request.Values;
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required to complete registration");
            }

            try { 
                await _userRepository.CompleteRegistration(_supabase, userId, data, ct);

                if((data.FollowingUsers?.Length ?? 0) > 0)
                    await FollowerInitialUsers(userId, data.FollowingUsers, ct);
                
                await _notificationRepository.CreateNotification(
                    _supabase,
                    userId: userId,
                    notificationMsg: "You Completed your account registration.",
                    notificationType: "complete_registration",
                    link: $"/users/{data.Username}",
                    entityType: "user",
                    relatedEntityId: userId,
                    ct: ct
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

        private async Task FollowerInitialUsers(Guid userId, Guid[] usersToFollow, CancellationToken ct)
        {
            var tasks = usersToFollow.Select(uTF =>
                                                    _userFollowRepository.AddUserFollow(_supabase, userId, new Domain.SocialMedia.User.FollowUserFormDto()
                                                    {
                                                        UserToFollowId = uTF
                                                    }, ct))
                                        .ToArray();
            await Task.WhenAll(tasks);
        }
    }
}