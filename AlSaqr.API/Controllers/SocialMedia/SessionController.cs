using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.SocialMedia.User;

namespace AlSaqr.API.Controllers.SocialMedia
{
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
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _userRepository = userRepository;
            _profileRepository = profileRepository;
            _supabase = supabase;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Signin or check neo4j data when signing in with supabase.
        /// </summary>
        /// <returns></returns>
        [HttpPost("signin")]
        public async Task<IActionResult> SignInWithSupabase([FromBody] Common.AlSaqrUpsertRequest<OAuthUserProfile> request)
        {
            var data = request.Values;
            
            // Input validation
            if (string.IsNullOrEmpty(data.Email))
            {
                return BadRequest("Enail is required");
            }

            try
            {


                var selectUserResult = _supabase.From<AlSaqrUser>()
                                            .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, data.Email)
                                            .Limit(1);

                var existingUser = (await selectUserResult.Get()).Models.FirstOrDefault();
            
                if (existingUser == null && !string.IsNullOrEmpty(data.Email))
                {

                    var isDiscordAccount = !string.IsNullOrEmpty(data.ProfileAvatar) ? data.ProfileAvatar.Contains("discord") : false;

                    var newUser = new CreateInitialUserDto()
                    {
                        Id = Guid.NewGuid(),
                        FirstName = data.UserMetadata.FullName?.Split(' ')[0] ?? "",
                        LastName = data.UserMetadata.FullName?.Split(' ').Length > 1 ? data.UserMetadata.FullName.Split(' ')[1] : null,
                        Username = isDiscordAccount ? data.GlobalName : data.DisplayName ?? GetEmailUsername(data.Email ?? ""),
                        Email = data.Email!,
                        CreatedAt = DateTime.UtcNow,
                        Bio = "",
                        CountryOfOrigin = "United States",
                        Phone = data.Phone,
                        Avatar = !string.IsNullOrEmpty(data.ProfileAvatar) ? data.ProfileAvatar
                                    : !string.IsNullOrEmpty(data?.UserMetadata?.Picture) ? data.UserMetadata?.Picture : data?.UserMetadata?.AvatarUrl ?? "",
                        BgThumbnail = CityBackgrounds.GetRandomCityImage(),
                        DateOfBirth = null,
                        Religion = "Muslim",
                        Hobbies = new string[] { },
                        FrequentMasjid = "",
                        FavoriteQuranReciters = new string[] { },
                        FavoriteIslamicScholars = new string[] { },
                        IslamicStudyTopics = new string[] { },
                        MaritalStatus = "Single",
                        PreferredMadhab = "Hanafi"
                    };

                    await _userRepository.CreateInitialUser(_supabase, newUser);
                }

                
                _logger.LogInformation("User signed in successfully!");
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Signin error!");
                return StatusCode(500, new { message = "Fetch User Signin error!", success = false });
            }
        }


        /// <summary>
        /// Check user if he's logged in.
        /// </summary>
        /// <returns></returns>
        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] Common.AlSaqrUpsertRequest<SessionCheckRequest> request)
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
                var (userId, username) = await _userRepository.GetUserIdAndUsernameByEmail(_supabase, data.Email);
                
                var sessionUserResult = await _profileRepository.GetSessionInfo(_supabase, userId);

                _logger.LogInformation("User signed in successfully!");

                if (sessionUserResult.Id == Guid.Empty || sessionUserResult.Id == null)
                    return BadRequest("Invalid user retrieved");

                _userCacheService.SetLoggedInUser(sessionUserResult);

                return Ok(new {  result = sessionUserResult });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Session error!");
                return StatusCode(500, new { message = "Fetch User Session error!", success = false });
            }
        }

    }
}
