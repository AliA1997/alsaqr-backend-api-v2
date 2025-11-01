
namespace AlSaqr.Domain.Meetup
{
    public class GroupItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public IDictionary<string, object>[]? Images { get; set; }

        public string? City { get; set; }
        public int CityId { get; set; }
        public IDictionary<string, object>[]? Attendees { get; set; }

    }
}
