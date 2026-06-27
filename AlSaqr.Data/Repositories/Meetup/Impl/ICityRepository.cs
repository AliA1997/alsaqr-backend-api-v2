using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface ICityRepository
    {
        Task<City> InsertOrRetrieveCity(Supabase.Client client, string cityName, string? stateOrProvince, string? country, decimal? latitude, decimal? longitude);
        Task InsertCityEvent(Supabase.Client client, Guid cityId, Guid eventId);

        /// <summary>
        /// Returns up to 100 distinct cities (by name) for dropdown selection. Not paginated.
        /// </summary>
        Task<List<CityDto>> GetCities(Supabase.Client client);
    }
}
