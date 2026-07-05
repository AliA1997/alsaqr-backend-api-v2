using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.SocialMedia
{
    /// <summary>
    /// Checks the logged-in user's session. Sign-in lives in <see cref="AuthController"/>.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly ILogger<SessionController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly Supabase.Client _supabase;
        private readonly IUserCacheService _userCacheService;

        public SessionController(
            ILogger<SessionController> logger,
            IUserRepository userRepository,
            IProfileRepository profileRepository,
            Supabase.Client supabase,
            IUserCacheService userCacheService
        )
        {
            _logger = logger;
            _userRepository = userRepository;
            _profileRepository = profileRepository;
            _supabase = supabase;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Check user if he's logged in.
        /// </summary>
        /// <returns></returns>
        [HttpPost("check")]
        public async Task<IActionResult> Check(
            [FromBody] Common.AlSaqrUpsertRequest<AlSaqr.Domain.SocialMedia.Session.SessionCheckRequest> request
        )
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(data.Email))
            {
                return BadRequest("Enail is required");
            }
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                var (userId, username) = await _userRepository.GetUserIdAndUsernameByEmail(
                    _supabase,
                    data.Email
                );

                var sessionUserResult = await _profileRepository.GetSessionInfo(_supabase, userId);

                _logger.LogInformation("User signed in successfully!");

                if (sessionUserResult.Id == Guid.Empty || sessionUserResult.Id == null)
                    return BadRequest("Invalid user retrieved");

                // Set (when passed in) and retrieve the user's web3 address; web3 users
                // display differently compared to normal oauth users.
                var web3Address = await _userRepository.SetWeb3Address(
                    _supabase,
                    userId,
                    data.Web3Address,
                    ct
                );
                sessionUserResult.Web3Address = !string.IsNullOrEmpty(web3Address) ? web3Address : sessionUserResult.Web3Address;
                sessionUserResult.IsWeb3 = !string.IsNullOrEmpty(web3Address);

                _userCacheService.SetLoggedInUser(sessionUserResult);

                return Ok(new { result = sessionUserResult });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Session error!");
                return StatusCode(
                    500,
                    new { message = "Fetch User Session error!", success = false }
                );
            }
        }
    }
}
