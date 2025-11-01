
using Newtonsoft.Json;

namespace AlSaqr.Domain.Meetup
{
    public class SimilarGroupDto: GroupDto
    {
        [JsonProperty("description_similarity")]
        public decimal DescriptionSimilarity { get; set; }
    }
}
