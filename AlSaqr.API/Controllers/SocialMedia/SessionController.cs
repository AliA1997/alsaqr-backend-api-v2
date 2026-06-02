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
        private readonly Supabase.Client _supabase;
        private readonly IUserCacheService _userCacheService;

        public SessionController(
            ILogger<SessionController> logger,  
            IUserRepository userRepository,
            Supabase.Client supabase,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _userRepository = userRepository;
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

                    var isDiscordAccount = !string.IsNullOrEmpty(data.ImageUrl) ? data.ImageUrl.Contains("discord") : false;

                    var newUser = new CreateInitialUserDto()
                    {
                        Id = Guid.NewGuid(),
                        FirstName = data.FirstName,
                        LastName = data.LastName,
                        Username = isDiscordAccount ? data.GlobalName : GetEmailUsername(data.Email ?? ""),
                        Email = data.Email!,
                        CreatedAt = DateTime.UtcNow,
                        Bio = "",
                        CountryOfOrigin = "United States",
                        Phone = null,
                        Avatar = data.Picture != null ? data.Picture : data?.ImageUrl,
                        BgThumbnail = GetRandomCityImage(),
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


            try
            {
                var userProfileResult = await _supabase.From<VwUserProfileInfo>()
                                            .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, data.Email)
                                            .Limit(1)
                                            .Get(); 
                var userProfilePosts = await _supabase.From<VwUserProfilePosts>()
                                            .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, data.Email)
                                            .Get();

                VwUserProfileInfo? userRes = userProfileResult?.Models.FirstOrDefault();
                List<VwUserProfilePosts>? userPostsRes = userProfilePosts.Models;

                if (userRes == null)
                    return BadRequest($"User not found for {data.Email}");

                _logger.LogInformation("User signed in successfully!");


                var sessionUser = new SessionUser(userRes)
                {

                    Bookmarks = userRes.BookmarkIds.ToArray(),
                    Reposts = userPostsRes.Where(u => u.PostRelationType == "repost").Select(u => u.PostId).ToArray(),
                    LikedPosts = userPostsRes.Where(u => u.PostRelationType == "liked").Select(u => u.PostId).ToArray()
                };
                var userId = sessionUser.Id;

                if (sessionUser.Id == Guid.Empty || sessionUser.Id == null)
                    return BadRequest("Invalid user retrieved");

                _userCacheService.SetLoggedInUser(sessionUser);

                return Ok(new {  result = sessionUser });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Session error!");
                return StatusCode(500, new { message = "Fetch User Session error!", success = false });
            }
        }

    }
}
