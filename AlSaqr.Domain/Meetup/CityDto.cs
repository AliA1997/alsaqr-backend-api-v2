namespace AlSaqr.Domain.Meetup
{
    /// <summary>
    /// Lightweight city shape for dropdowns. Mirrors the front-end <c>City</c> interface:
    /// serialized camelCase as { id, name, stateOrProvince, country, latitude, longitude }.
    /// </summary>
    /// <remarks>
    /// <c>Id</c> is the city's <see cref="System.Guid"/> primary key (the table keys on a
    /// GUID, not an integer), so the JSON <c>id</c> is a string. State/province, latitude
    /// and longitude are optional and may be omitted by the source row.
    /// </remarks>
    public class CityDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? StateOrProvince { get; set; }

        public string Country { get; set; } = string.Empty;

        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }
    }
}
