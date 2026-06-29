using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class CityRepository : ICityRepository
    {
        public CityRepository() { }

        public async Task<City> InsertOrRetrieveCity(
            Supabase.Client client,
            string cityName,
            string? stateOrProvince,
            string? country,
            decimal? latitude,
            decimal? longitude
        )
        {
            City? city = null;
            try
            {
                city = (
                    await client.From<City>().Filter("name", Operator.Equals, cityName).Get()
                ).Model;
                if (city == null)
                {
                    //var recentlyInsertedCityId = await client.From<City>().Count(CountType.Estimated);
                    city = (
                        await client
                            .From<City>()
                            .Upsert(
                                new City()
                                {
                                    Id = Guid.NewGuid(),
                                    Name = cityName,
                                    StateOrProvince = stateOrProvince,
                                    Country = country,
                                    Latitude = latitude,
                                    Longitude = longitude,
                                },
                                new QueryOptions()
                                {
                                    Returning = QueryOptions.ReturnType.Representation,
                                }
                            )
                    ).Model;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting city in repository layer:", ex.Message);
            }

            return city!;
        }

        public async Task<List<CityDto>> GetCities(Supabase.Client client, string searchTerm)
        {
            // Reference data for a dropdown — no pagination. Deterministic ordering
            // (name, then the unique id as a tie-breaker) keeps the 100-row window stable.
            var response = await client
                .From<City>()
                .Where(c => c.Name != null)
                .Filter("name", Operator.ILike, $"%{searchTerm}%")
                .Order("name", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get();

            return response
                .Models.DistinctBy(c => c.Name)
                .Select(c => new CityDto
                {
                    Id = c.Id,
                    Name = c.Name ?? string.Empty,
                    StateOrProvince = c.StateOrProvince,
                    Country = c.Country ?? string.Empty,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                })
                .ToList();
        }

        public async Task InsertCityEvent(Supabase.Client client, Guid cityId, Guid eventId)
        {
            
            var andFilters = new List<IPostgrestQueryFilter>()
            {
                new QueryFilter("event_id", Operator.Equals, eventId),
            };
            var cityEvent = (
                await client
                    .From<EventCities>()
                    .Filter("city_id", Operator.Equals, cityId.ToString())
                    .And(andFilters)
                    .Get()
            ).Model;
            if (cityEvent == null)
                await client
                    .From<EventCities>()
                    .Upsert(
                        new EventCities()
                        {
                            Id = Guid.NewGuid(),
                            CityId = cityId,
                            EventId = eventId,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );

            return;
        }

        public async Task<CityDto> GetCitiesById(Supabase.Client client, Guid cityId)
        {

            var cityById = await client
                .From<City>()
                .Filter("id", Operator.Equals, cityId.ToString())
                .Single();

            return new CityDto
            {
                Id = cityById.Id,
                Name = cityById.Name ?? string.Empty,
                StateOrProvince = cityById.StateOrProvince,
                Country = cityById.Country ?? string.Empty,
                Latitude = cityById.Latitude,
                Longitude = cityById.Longitude,
            };
        }
    }
}
