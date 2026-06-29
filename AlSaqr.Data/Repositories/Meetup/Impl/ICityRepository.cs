using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface ICityRepository
    {
        Task<City> InsertOrRetrieveCity(
            Supabase.Client client,
            string cityName,
            string? stateOrProvince,
            string? country,
            decimal? latitude,
            decimal? longitude
        );
        Task InsertCityEvent(Supabase.Client client, Guid cityId, Guid eventId);

        Task<List<CityDto>> GetCities(Supabase.Client client, string searchTerm);

        Task<CityDto> GetCitiesById(Supabase.Client client, Guid cityId);
    }
}
