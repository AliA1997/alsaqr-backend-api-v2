using AlSaqr.Data.Repositories.Yumna.Impl;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.Yumna;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Yumna.YumnaAgent;

namespace AlSaqr.API.Controllers.Yumna
{
    [ApiController]
    [Route("[controller]")]
    public class YumnaController : AuthorizedControllerBase
    {
        private const string AgentName = "Yumna";
        private const int DefaultDailyRequestLimit = 30;

        private readonly ILogger<YumnaController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IYumnaService _yumnaService;
        private readonly IUserCacheService _userCacheService;

        public YumnaController(
            ILogger<YumnaController> logger,
            Supabase.Client supabase,
            ISubscriptionRepository subscriptionRepository,
            IYumnaService yumnaService,
            IUserCacheService userCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _subscriptionRepository = subscriptionRepository;
            _yumnaService = yumnaService;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Send a prompt to the Yumna AI agent. Limited to the user's daily
        /// subscription allowance (30 prompt responses per day by default).
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("prompt")]
        public async Task<IActionResult> PromptYumna(
            [FromBody] AlSaqrUpsertRequest<PromptMessageDto> request
        )
        {
            // var authError = ValidateAccessToken();
            // if (authError != null)
            //     return authError;

            var data = request.Values;

            if (data == null || string.IsNullOrEmpty(data.Prompt))
            {
                return BadRequest("Missing required fields");
            }

            // var loggedInUser = _userCacheService.GetLoggedInUser();
            // Guid.TryParse(loggedInUser?.Id?.ToString(), out Guid userId);

            // if (userId == Guid.Empty)
            // {
            //     return Unauthorized("You must be logged in to message Yumna.");
            // }

            // var subscription = await _subscriptionRepository.GetUserSubscription(_supabase, userId);
            // var dailyLimit = subscription?.DailyRequestLimit ?? DefaultDailyRequestLimit;

            // var dailyUse = await _subscriptionRepository.GetDailyUse(_supabase, userId);

            // if (dailyUse >= dailyLimit)
            // {
            //     return BadRequest($"You are over your daily use limit of {dailyLimit} requests. Try again tomorrow.");
            // }

            var prompt = PromptUtility.BuildStandardPrompt(
                AgentName,
                data.Prompt,
                // loggedInUser?.Username,
                "Ali",
                data.Context);

            var result = await _yumnaService.GetAgentResponse(prompt);
            _logger.LogInformation("Yumna responded to a prompt.");

            // var updatedDailyUse = await _subscriptionRepository.UpdateDailyUse(_supabase, userId);

            return Ok(new YumnaResponseDto
            {
                Result = result,
                // DailyUse = updatedDailyUse,
                // DailyLimit = dailyLimit
            });
        }
    }
}
