using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class ListsController : ControllerBase
    {

        private readonly ILogger<ListsController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IListRepository _listRepository;
        private readonly IListItemRepository _listItemRepository;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public ListsController(
            ILogger<ListsController> logger, 
            Supabase.Client supabase,
            IListRepository listRepository,
            IListItemRepository listItemRepository,
            ISocialMediaCacheService socialMediaCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _listRepository = listRepository;
            _listItemRepository = listItemRepository;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Get the lists for the logged in user
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetLists(
            Guid userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            var lists = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            if (_socialMediaCacheService.CheckIfInitialListsCanBeRetrieved(currentPage, userId))
                return Ok(_socialMediaCacheService.GetInitialLists(userId));

            var result = await _listRepository.GetLists(_supabase, userId, searchTerm, currentPage, itemsPerPage);
            _socialMediaCacheService.SetInitialLists(result, userId);

            return Ok(result);
        }

        /// <summary>
        /// Create a list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateList(
                [FromRoute] Guid userId,
                [FromBody] AlSaqrUpsertRequest<List.CreateListFormDto> request)
        {
            var data = request.Values;
            if (userId == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }

            if (string.IsNullOrEmpty(data?.Name))
            {
                return BadRequest("Name of List is required");
            }


            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;


            try
            {
                var newlyCreatedListId = await _listRepository.CreateList(_supabase, userId, data, ct);

                // Add users added
                if (data.UsersAdded != null && data.UsersAdded.Length > 0)
                {
                    await _listItemRepository.AddUsersToList(_supabase, newlyCreatedListId, data.UsersAdded.ToList(), ct);
                }

                // Add posts added
                if (data.PostsAdded != null && data.PostsAdded.Length > 0)
                {
                    await _listItemRepository.AddPostsToList(_supabase, newlyCreatedListId, data.PostsAdded.ToList(), ct);
                }

                _socialMediaCacheService.ClearInitialLists(userId);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating list: {err.Message}");
                return StatusCode(500, new { message = "Add list error!", success = false });
            }
        }


        /// <summary>
        /// Get the list items for a given list.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{listId}")]
        public async Task<IActionResult> GetListItems(
            Guid userId,
            Guid listId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            if (listId == Guid.Empty)
                return BadRequest("Must have a list id to get saved list items.");

            var result = await _listItemRepository.GetListItems(_supabase, userId, listId, currentPage, itemsPerPage);

            return Ok(result);
        }

        /// <summary>
        /// Save item to the list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{listId}")]
        public async Task<IActionResult> SavedItemToList(
            string userId,
            string listId,
            [FromBody] AlSaqrUpsertRequest<List.SaveItemToListDto> request)
        {
            var data = request.Values;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(listId))
                return BadRequest("Missing required fields");

            if (!Guid.TryParse(userId, out var userGuid))
                return BadRequest("User ID must be a valid GUID");

            if (!Guid.TryParse(listId, out var listGuid))
                return BadRequest("List ID must be a valid GUID");

            if (string.IsNullOrEmpty(data.Type))
                return BadRequest("Item type is required");

            if (data.RelatedEntityId == Guid.Empty)
                return BadRequest("Related entity ID is required");

            using var cts = new CancellationTokenSource();

            try
            {
                await _listItemRepository.SaveItemToList(_supabase, userGuid, listGuid, data, cts.Token);

                return Ok(new { success = true, message = "Saved item to list Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Saved item to list error!");
                return StatusCode(500, new { message = "Saved item to list error!", success = false });
            }
        }

        /// <summary>
        /// Delete list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{listId}")]
        public async Task<IActionResult> DeleteList(
            Guid userId,
            Guid listId)
        {
            
            // Input validation
            if(userId == Guid.Empty || listId == Guid.Empty)
            {
                return BadRequest("Missing required fields such as user id or list id.");
            }

            await _listRepository.DeleteList(_supabase, userId, listId);
  
            return Ok(new { success = true });
    
        }

        /// <summary>
        /// Delete saved item from list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{listId}/{listItemId}")]
        public async Task<IActionResult> DeleteSavedFromList(
            Guid userId,
            Guid listId,
            Guid listItemId)
        {

            // Input validation
            if (userId == Guid.Empty || listId == Guid.Empty || listItemId == Guid.Empty)
            {
                return BadRequest("Missing required fields such as user id or list id.");
            }


            await _listItemRepository.DeleteListItem(_supabase, listId, listItemId);

            return Ok(new { success = true });
        }


    }
}
