using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class GroupsController : AuthorizedControllerBase
    {
        private readonly ILogger<GroupsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IAttendeeRepository _attendeeRepository;
        private readonly ICityRepository _cityRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly ITopicRepository _topicRepository;


        public GroupsController(
            ILogger<GroupsController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            IAttendeeRepository attendeeRepository,
            ICityRepository cityRepository,
            IGroupRepository groupRepository,
            ITopicRepository topicRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _attendeeRepository = attendeeRepository;
            _cityRepository = cityRepository;
            _groupRepository = groupRepository;
            _topicRepository = topicRepository;
        }

        /// <summary>
        /// Get groups nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetNearbyGroups(
                [FromQuery] string latitude,
                [FromQuery] string longitude,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null,
                [FromQuery] double? maxDistanceKm = 25.0
            )
        {
            var result = await _groupRepository.GetNearbyGroups(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm);

            return Ok(result);
        }

        /// <summary>
        /// Get my events nearby
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <param name="maxDistanceKm"></param>
        /// <returns></returns>
        [HttpGet("my")]
        public async Task<IActionResult> GetNearbyMyGroups(
                [FromQuery] string latitude,
                [FromQuery] string longitude,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 25,
                [FromQuery] string? searchTerm = null,
                [FromQuery] double? maxDistanceKm = 25.0
            )
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("Need to be logged in to see your events or groups.");

            var result = await _groupRepository.GetMyGroups(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                loggedInUser.Id.ToString(),
                searchTerm,
                maxDistanceKm);

            return Ok(result);
        }

        /// <summary>
        /// Create a group
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateGroup(
            [FromBody] AlSaqrUpsertRequest<CreateGroupForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var data = request.Values;

            if (string.IsNullOrEmpty(data.Name) || string.IsNullOrEmpty(data.Description) || data.HqCity == null)
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                if (loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a group.");
                }
                Guid.TryParse(loggedInUser?.Id.ToString(), out var userId);

                var city = await _cityRepository.InsertOrRetrieveCity(_supabase, data.HqCity, data.HqStateOrProvince, data.HqCountry, data.HqLatitude, data.HqLongitude);

                var organizerAttendee = await _attendeeRepository.InsertOrRetrieveAttendee(_supabase, $"{loggedInUser.FirstName + " " + loggedInUser.LastName}", loggedInUser.Id ?? Guid.Empty);

                var recentInsertedId = await _supabase.From<Groups>().Count(CountType.Estimated);
                var recentInsertedGroupAttendee = await _supabase.From<GroupAttendees>().Count(CountType.Estimated);

                var insertedGroup = await _groupRepository.CreateGroup(_supabase, data, userId, organizerAttendee.Id, city.Id, ct);

                await _attendeeRepository.InsertGroupAttendees(_supabase, insertedGroup.Id!, (data.Attendees ?? new Dictionary<string, object>[] { }).ToList());

                await _topicRepository.InsertGroupTopics(_supabase, insertedGroup.Id!, (data.Topics ?? new Dictionary<string, object>[] { }).ToList());

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating group: {err.Message}");
                return StatusCode(500, new { message = "Add group error!", success = false });
            }
        }

        /// <summary>
        /// Update a group. Only the group founder may update it.
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{groupId:guid}")]
        public async Task<IActionResult> UpdateGroup(
            Guid groupId,
            [FromBody] AlSaqrUpsertRequest<UpsertGroupForm> request)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;
            var data = request.Values;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to update a group.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                Guid? cityId = null;
                if (!string.IsNullOrEmpty(data.HqCity))
                {
                    var city = await _cityRepository.InsertOrRetrieveCity(
                        _supabase, data.HqCity, data.HqStateOrProvince, data.HqCountry, data.HqLatitude, data.HqLongitude);
                    cityId = city.Id;
                }

                await _groupRepository.UpdateGroup(_supabase, groupId, userId, data, cityId, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = err.Message, success = false });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error updating group: {err.Message}");
                return StatusCode(500, new { message = "Update group error!", success = false });
            }
        }

        /// <summary>
        /// Delete a group. Only the group founder may delete it.
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpDelete("{groupId:guid}")]
        public async Task<IActionResult> DeleteGroup(Guid groupId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to delete a group.");
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                await _groupRepository.DeleteGroup(_supabase, groupId, userId, ct);
                return Ok(new { success = true });
            }
            catch (UnauthorizedAccessException err)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = err.Message, success = false });
            }
            catch (Exception err)
            {
                Console.WriteLine($"Error deleting group: {err.Message}");
                return StatusCode(500, new { message = "Delete group error!", success = false });
            }
        }
    }
}
