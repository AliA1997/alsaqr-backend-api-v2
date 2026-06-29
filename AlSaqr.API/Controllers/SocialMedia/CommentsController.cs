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
    public class CommentsController : AuthorizedControllerBase
    {

        private readonly ILogger<CommentsController> _logger;
        private Supabase.Client _supabase;
        private readonly ICommentRepository _commentsRepository;
        private readonly IUserCacheService _userCacheService;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public CommentsController(
            ILogger<CommentsController> logger, 
            Supabase.Client supabase,
            ICommentRepository commentRepository,
            IUserCacheService userCacheService,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _commentsRepository = commentRepository;
            _userCacheService = userCacheService;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Create a comment
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateComment(
            [FromBody] AlSaqrUpsertRequest<Posts.CreateCommentDto> request,
            [FromQuery] bool onComment = false)
        {

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;
            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            if (string.IsNullOrEmpty(data?.Text))
                return BadRequest("Text of the Comment is required");
            if (data.PostId == Guid.Empty)
                return BadRequest("Post Id is required to create a comment.");

            var cts = new CancellationTokenSource();
            var ct = cts.Token;
            await _commentsRepository.CreateComment(
                _supabase,
                userId,
                data.PostId,
                data,
                ct
            );

            _logger.LogInformation("Comment created Successfully for Post {postId}", data.PostId);
            _socialMediaCacheService.ClearInitialComments(data.PostId);

            return Ok(new { success = true });
            
        }

    }
}
