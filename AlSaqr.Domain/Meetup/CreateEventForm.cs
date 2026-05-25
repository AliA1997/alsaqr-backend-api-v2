namespace AlSaqr.Domain.Meetup
{

    public class CreateEventForm
    {

        public string? Name { get; set; }
        public string? Description { get; set; }

        public Guid? GroupId { get; set; }
        public string[]? Images { get; set; }
        public bool IsOnline { get; set; }
        public DateTime DateToOccur { get; set; }
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? Country { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

    }

    public class UpsertEventForm : CreateEventForm
    {
        public string[] FieldsToUpdate { get; set; }
    }
}
