using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.Utils;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.SocialMedia.Session;
using static AlSaqr.Domain.SocialMedia.User;

namespace AlSaqr.API.Controllers.SocialMedia
{
    /// <summary>
    /// Handles sign-in (OAuth and web3). Session checks live in <see cref="SessionController"/>.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IUserRepository _userRepository;
        private readonly Supabase.Client _supabase;

        public AuthController(
            ILogger<AuthController> logger,
            IUserRepository userRepository,
            Supabase.Client supabase
        )
        {
            _logger = logger;
            _userRepository = userRepository;
            _supabase = supabase;
        }

        /// <summary>
        /// Signin or check user data when signing in with supabase (OAuth) or a web3 wallet.
        /// </summary>
        /// <returns></returns>
        [HttpPost("signin")]
        public async Task<IActionResult> SignInWithSupabase(
            [FromBody] Common.AlSaqrUpsertRequest<OAuthUserProfile> request
        )
        {
            var data = request.Values;

            // Input validation
            if (string.IsNullOrEmpty(data.Email) && string.IsNullOrEmpty(data.Web3Address))
            {
                return BadRequest("Email or web3 address is required");
            }

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                AlSaqrUser? existingUser = null;

                // Web3 sign-in: the wallet address identifies the account, like email does.
                if (!string.IsNullOrEmpty(data.Web3Address))
                {
                    existingUser = await _userRepository.GetUserByWeb3Address(
                        _supabase,
                        data.Web3Address
                    );
                }

                if (existingUser == null && !string.IsNullOrEmpty(data.Email))
                {
                    existingUser = await _userRepository.GetUserByEmail(_supabase, data.Email);

                    // Existing email account signing in with a wallet — record the address.
                    if (existingUser != null && !string.IsNullOrEmpty(data.Web3Address))
                    {
                        await _userRepository.SetWeb3Address(
                            _supabase,
                            existingUser.Id,
                            data.Web3Address,
                            ct
                        );
                    }
                }

                if (existingUser == null)
                {
                    var isDiscordAccount = !string.IsNullOrEmpty(data.ProfileAvatar)
                        ? data.ProfileAvatar.Contains("discord")
                        : false;

                    var newUser = new CreateInitialUserDto()
                    {
                        Id = Guid.NewGuid(),
                        FirstName = data.UserMetadata?.FullName?.Split(' ')[0] ?? "",
                        LastName =
                            data.UserMetadata?.FullName?.Split(' ').Length > 1
                                ? data.UserMetadata.FullName.Split(' ')[1]
                                : null,
                        Username = isDiscordAccount
                            ? data.GlobalName
                            : data.DisplayName
                                ?? (
                                    !string.IsNullOrEmpty(data.Email)
                                        ? GetEmailUsername(data.Email)
                                        : data.Web3Address
                                ),
                        Email = data.Email!,
                        CreatedAt = DateTime.UtcNow,
                        Bio = "",
                        CountryOfOrigin = "United States",
                        Phone = data.Phone,
                        Avatar =
                            !string.IsNullOrEmpty(data.ProfileAvatar) ? data.ProfileAvatar
                            : !string.IsNullOrEmpty(data?.UserMetadata?.Picture)
                                ? data.UserMetadata?.Picture
                            : data?.UserMetadata?.AvatarUrl ?? "",
                        BgThumbnail = CityBackgrounds.GetRandomCityImage(),
                        DateOfBirth = null,
                        Religion = "Muslim",
                        Hobbies = new string[] { },
                        FrequentMasjid = "",
                        FavoriteQuranReciters = new string[] { },
                        FavoriteIslamicScholars = new string[] { },
                        IslamicStudyTopics = new string[] { },
                        MaritalStatus = "Single",
                        PreferredMadhab = "Hanafi",
                        Web3Address = data?.Web3Address,
                    };

                    await _userRepository.CreateInitialUser(_supabase, newUser);
                }

                _logger.LogInformation("User signed in successfully!");
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Signin error!");
                return StatusCode(
                    500,
                    new { message = "Fetch User Signin error!", success = false }
                );
            }
        }
    }
}
