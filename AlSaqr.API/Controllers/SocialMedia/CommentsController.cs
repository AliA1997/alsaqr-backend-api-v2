using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommentsController : ControllerBase
    {

        private readonly ILogger<CommentsController> _logger;
        private Supabase.Client _supabase;
        private readonly ICommentRepository _commentsRepository;
        private readonly IUserCacheService _userCacheService;


        public CommentsController(
            ILogger<CommentsController> logger, 
            Supabase.Client supabase,
            ICommentRepository commentRepository,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _commentsRepository = commentRepository;
            _userCacheService = userCacheService;
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
            var data = request.Values;

            if (data.UserId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

           
            if (string.IsNullOrEmpty(data?.Text))
            {
                return BadRequest("Text of the Comment is required");
            }

            var loggedInUser = _userCacheService.GetLoggedInUser();

            if(loggedInUser == null)
            {
                return Unauthorized("User must be logged in, in order to create a comment.");
            }

            await _commentsRepository.CreateComment(
                _supabase,
                loggedInUser?.Id ?? Guid.Empty,
                data.PostId,
                data
            );

            _logger.LogInformation("Comment created Successfully for Post {postId}", data.PostId);

            return Ok(new { success = true });
            
        }

    }
}
