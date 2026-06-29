using AlSaqr.Data.Repositories.Meetup.Impl;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers.Meetup
{
    [ApiController]
    [Route("[controller]")]
    public class TopicsController : ControllerBase
    {
        private readonly ILogger<TopicsController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly ITopicRepository _topicRepository;

        public TopicsController(
            ILogger<TopicsController> logger,
            Supabase.Client supabase,
            ITopicRepository topicRepository)
        {
            _logger = logger;
            _supabase = supabase;
            _topicRepository = topicRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetTopics()
        {
            var result = await _topicRepository.GetTopics(_supabase);
            return Ok(result);
        }
    }
}
