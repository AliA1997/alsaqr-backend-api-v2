using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Supabase.Postgrest.Interfaces;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class GroupDetailsController : ControllerBase 
    {
        private readonly ILogger<GroupDetailsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;

        public GroupDetailsController(
            ILogger<GroupDetailsController> logger,
            IDriver driver,
            Supabase.Client supabase,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
            _supabase = supabase;
            _userCacheService = userCacheService;
        }


        /// <summary>
        /// Get group details
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupDetails(int groupId)
        {
            var events = new List<EventDto>();
            GroupDto? groupDetails = null;
            IPostgrestTable<VwGroup>? selectGroupResult = null;
            IPostgrestTable<VwEvent>? selectEventResult = null;
            try
            {
                selectEventResult = _supabase.From<VwEvent>()
                                        .Filter("group_id", Supabase.Postgrest.Constants.Operator.Equals, groupId)
                                        .Limit(10);
                selectGroupResult = _supabase.From<VwGroup>()
                                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, groupId)
                                        .Limit(1);

                events = (await selectEventResult.Get()).Models.Select(sr => new EventDto()
                {
                    Id = sr.Id,
                    GroupId = sr.GroupId,
                    GroupName = sr.GroupName,
                    Name = sr.Name,
                    Description = sr.Description,
                    CitiesHosted = sr.CitiesHosted,
                    Images = sr.Images,
                    DistanceKm = 0
                }).ToList();
                groupDetails = (await selectGroupResult.Get()).Models.Select(sr => new GroupDto()
                {
                    Id = sr.Id,
                    Name = sr.Name,
                    Description = sr.Description,
                    City = sr.HqCity,
                    CityId = sr.HqCityId,
                    Country = sr.HqCountry,
                    Attendees = sr.Attendees,
                    Images = sr.Images,
                    Topics = sr.Topics
                }).First();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { events, groupDetails, success = true });
        }
    }
}
