

namespace AlSaqr.Domain.Meetup
{
    public class CreateGroupForm
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string[]? Images { get; set; }
        public int? HqCityId { get; set; }
        public string? HqCity { get; set; }
        public string? HqStateOrProvince { get; set; }
        public string? HqCountry { get; set; }
        public decimal? HqLatitude { get; set; }
        public decimal? HqLongitude { get; set; }
        public string[] Topics { get; set; }
        public IDictionary<string, object>[]? Attendees { get; set; }
    }

    public class UpsertGroupForm : CreateGroupForm
    {}
}
