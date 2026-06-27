namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// A city a local guide hosts in. Mirrors the loose city shape used by groups/events
    /// so the controller can resolve (or insert) the corresponding City row before the
    /// local_guides_cities link is written.
    /// </summary>
    public class LocalGuideCityForm
    {
        public string? City { get; set; }
        public string? StateOrProvince { get; set; }
        public string? Country { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class CreateLocalGuideForm
    {
        public string? Name { get; set; }
        public LocalGuideCityForm[]? Cities { get; set; }
    }

    public class UpsertLocalGuideForm : CreateLocalGuideForm
    {
        public string[] FieldsToUpdate { get; set; }
    }
}
