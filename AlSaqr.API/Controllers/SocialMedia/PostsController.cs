using AlSaqr.Data.Helpers;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class PostsController : ControllerBase
    {

        private readonly ILogger<PostsController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IPostRepository _postRepository;
        private readonly ICommentRepository _commentRepository;
        private readonly IPostStatusRepository _postStatusRepository;
        private readonly IUserCacheService _userCacheService;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public PostsController(
            ILogger<PostsController> logger, 
            Supabase.Client supabase,
            IPostRepository postRepository,
            ICommentRepository commentRepository,
            IPostStatusRepository postStatusRepository,
            IUserCacheService userCacheService,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _postRepository = postRepository;
            _commentRepository= commentRepository;
            _postStatusRepository = postStatusRepository;
            _userCacheService = userCacheService;
            _socialMediaCacheService = socialMediaCacheService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPosts(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {

            if(_socialMediaCacheService.CheckIfInitialPostsCanBeRetrieved(currentPage))
                return Ok(_socialMediaCacheService.GetInitialPosts());

            var result = await _postRepository.GetPosts(_supabase, searchTerm, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Create a post
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] AlSaqrUpsertRequest<Posts.CreatePostDto> request)
        {
            var data = request.Values;
            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to create a post.");

            if (data.UserId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

            if (string.IsNullOrEmpty(data?.Text))
            {
                return BadRequest("Text of the Post is required");
            }

            
            await _postRepository.CreatePost(
                _supabase,
                loggedInUser.Id ?? Guid.Empty,
                data
            );

            _socialMediaCacheService.ClearInitialPosts();

            return Ok(new { success = true });
            
        }

        /// <summary>
        /// Get a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpGet("{postId}")]
        public async Task<IActionResult> GetPost(Guid postId)
        {
            // Input validation
            if (postId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }

            var post = await _postRepository.GetPost(_supabase, postId);
            return Ok(new { post, success = true });
            
        }

        /// <summary>
        /// Get comments for a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpGet("{postId}/comments")]
        public async Task<IActionResult> GetPostComments(
                Guid postId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10)
        {
            // Input validation
            if (postId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }

            var result = await _commentRepository.GetComments(_supabase, postId, currentPage, itemsPerPage);

            return Ok(result);
        }


        /// <summary>
        /// Bookmark for a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/bookmark")]
        public async Task<IActionResult> BookmarkPost(
            string postId,
            [FromBody] AlSaqrUpsertRequest<BookmarkRequest> request)
        {
            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var data = request.Values;
            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("Need to be logged in to bookmark posts.");

            var userId = user.Id;
            // Input validation
            if (data.StatusId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }

            await _postStatusRepository.BookmarkPost(_supabase, userId ?? Guid.Empty, data.StatusId, data.Bookmarked, ct);

            return Ok(new { success = true });
        }


        /// <summary>
        /// Like a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/liked")]
        public async Task<IActionResult> LikedPost(
            string postId, 
            [FromBody] AlSaqrUpsertRequest<LikeRequest> request)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;

            if (data.StatusId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }
            
            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("User must be logged in to like a post");

            var userId = user.Id;
            
            await _postStatusRepository.LikePost(_supabase, userId ?? Guid.Empty, data.StatusId, data.Liked, ct);

            return Ok(new { success = true });
   
        }

        /// <summary>
        /// Repost a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/repost")]
        public async Task<IActionResult> RePostPost(
            string postId, 
            [FromBody] AlSaqrUpsertRequest<RePostRequest> request)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var data = request.Values;
            // Input validation
            if (data.StatusId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }
            var user = _userCacheService.GetLoggedInUser();
            if (user == null)
                return Unauthorized("Must be logged in to repost a post.");

            var userId = user.Id;

            await _postStatusRepository.RepostPost(_supabase, userId ?? Guid.Empty, data.StatusId, data.Reposted, ct);

            return Ok(new { success = true });
            
        }

        /// <summary>
        /// Delete a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpDelete("{postId}")]
        public async Task<IActionResult> DeletePost(Guid postId)
        {
            // Input validation
            if (postId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }
            var user = _userCacheService.GetLoggedInUser();
            if (user == null || user.Id == Guid.Empty)
                return Unauthorized("Must be logged in to delete a post.");

            await _postRepository.DeletePost(_supabase, (Guid)user.Id, postId);
           return Ok(new { success = true });
        }

    }
}
