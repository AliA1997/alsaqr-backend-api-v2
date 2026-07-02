using AlSaqr.Data.Helpers;
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
    public class PostsController : AuthorizedControllerBase
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
            ISocialMediaCacheService socialMediaCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _postRepository = postRepository;
            _commentRepository = commentRepository;
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
            var loggedInUser = _userCacheService.GetLoggedInUser();
            var userId = loggedInUser?.Id ?? Guid.Empty;

            var noSearchTerm = string.IsNullOrEmpty(searchTerm ?? "".Trim());
            if (
                noSearchTerm
                && _socialMediaCacheService.CheckIfInitialPostsCanBeRetrieved(userId, currentPage)
            )
                return Ok(_socialMediaCacheService.GetInitialPosts(userId, currentPage));

            var result = await _postRepository.GetPosts(
                _supabase,
                searchTerm,
                currentPage,
                itemsPerPage
            );
            if (noSearchTerm)
                _socialMediaCacheService.SetInitialPosts(userId, currentPage, result);

            return Ok(result);
        }

        /// <summary>
        /// Create a post
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreatePost(
            [FromBody] AlSaqrUpsertRequest<Posts.CreatePostDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;
            var loggedInUser = _userCacheService.GetLoggedInUser();
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            if (string.IsNullOrEmpty(data?.Text))
            {
                return BadRequest("Text of the Post is required");
            }

            await _postRepository.CreatePost(_supabase, userId, data, ct);

            _socialMediaCacheService.ClearInitialPosts(userId, 0);

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
            [FromQuery] int itemsPerPage = 10
        )
        {
            // Input validation
            if (postId == Guid.Empty)
                return BadRequest("Post ID is required");

            if (_socialMediaCacheService.CheckIfInitialCommentsCanBeRetrieved(postId))
                return Ok(_socialMediaCacheService.GetInitialComments(postId));

            var result = await _commentRepository.GetComments(
                _supabase,
                postId,
                currentPage,
                itemsPerPage
            );

            _socialMediaCacheService.SetInitialComments(postId, result);

            return Ok(result);
        }

        /// <summary>
        /// Bookmark for a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/bookmark")]
        public async Task<IActionResult> BookmarkPost(
            Guid postId,
            [FromBody] AlSaqrUpsertRequest<BookmarkRequest> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var data = request.Values;
            var user = _userCacheService.GetLoggedInUser();
            var userId = user?.Id;
            // Input validation
            if (postId == Guid.Empty)
                return BadRequest("Post ID is required");

            await _postStatusRepository.BookmarkPost(
                _supabase,
                userId ?? Guid.Empty,
                postId,
                data.Bookmarked,
                ct
            );

            return Ok(new { success = true });
        }

        /// <summary>
        /// Like a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/liked")]
        public async Task<IActionResult> LikedPost(
            Guid postId,
            [FromBody] AlSaqrUpsertRequest<LikeRequest> request
        )
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;

            if (postId == Guid.Empty)
                return BadRequest("Post ID is required");

            var user = _userCacheService.GetLoggedInUser();
            var userId = user?.Id;

            await _postStatusRepository.LikePost(
                _supabase,
                userId ?? Guid.Empty,
                postId,
                data.Liked,
                ct
            );

            return Ok(new { success = true });
        }

        /// <summary>
        /// Repost a specific post
        /// </summary>
        /// <param name="postId"></param>
        /// <returns></returns>
        [HttpPatch("{postId}/repost")]
        public async Task<IActionResult> RePostPost(
            Guid postId,
            [FromBody] AlSaqrUpsertRequest<RePostRequest> request
        )
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;
            // Input validation
            if (postId == Guid.Empty)
                return BadRequest("Post ID is required");

            var user = _userCacheService.GetLoggedInUser();
            var userId = user?.Id;

            await _postStatusRepository.RepostPost(
                _supabase,
                userId ?? Guid.Empty,
                postId,
                data.Reposted,
                ct
            );

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
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            // Input validation
            if (postId == Guid.Empty)
            {
                return BadRequest("Post ID is required");
            }
            var user = _userCacheService.GetLoggedInUser();

            await _postRepository.DeletePost(_supabase, user?.Id ?? Guid.Empty, postId);
            return Ok(new { success = true });
        }
    }
}
