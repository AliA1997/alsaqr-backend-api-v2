using Microsoft.AspNetCore.Mvc;
using AlSaqr.Infrastructure;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class BookmarksController : ControllerBase
    {

        private readonly ILogger<BookmarksController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IUserCacheService _userCacheService;
        private readonly IPostRepository _postRepository;
        public BookmarksController(
            ILogger<BookmarksController> logger, 
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            IPostRepository postRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _postRepository = postRepository;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetBookmarks(
                Guid userId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {


            var result = await _postRepository.GetBookmarkedPosts(_supabase, userId, searchTerm, currentPage, itemsPerPage);

            return Ok(result);
        }

    }
}
