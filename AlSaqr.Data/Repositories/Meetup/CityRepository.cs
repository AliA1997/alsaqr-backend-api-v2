using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Supabase.Postgrest.Constants;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class CityRepository: ICityRepository
    {
        public CityRepository() { }

        public async Task<City> InsertOrRetrieveCity(Supabase.Client client, string cityName, string? stateOrProvince, string? country, decimal? latitude, decimal? longitude)
        {
            City? city = null;
            try
            {
                city = (await client.From<City>().Filter("name", Operator.Equals, cityName).Get()).Model;
                if (city == null)
                {
                    var recentlyInsertedCityId = await client.From<City>().Count(CountType.Estimated);
                    city = (
                        await client.From<City>().Upsert(new City()
                        {
                            Id = recentlyInsertedCityId + 1,
                            Name = cityName,
                            StateOrProvince = stateOrProvince,
                            Country = country,
                            Latitude = latitude,
                            Longitude = longitude
                        }, new QueryOptions() { Returning = QueryOptions.ReturnType.Representation })).Model;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting city in repository layer:", ex.Message);
            }

            return city!;
        }

        public async Task InsertCityEvent(Supabase.Client client, int cityId, int eventId)
        {
            var recentInsertedEventCityId = await client.From<EventCities>().Count(CountType.Estimated);
            var andFilters = new List<IPostgrestQueryFilter>()
            {
                 new QueryFilter("event_id", Operator.Equals, eventId)
            };
            var cityEvent = (await client.From<EventCities>().Filter("city_id", Operator.Equals, cityId)
                                                            .And(andFilters).Get()).Model;
            if(cityEvent == null)
                await client.From<EventCities>().Upsert(
                    new EventCities()
                    {
                        Id = recentInsertedEventCityId + 1,
                        CityId = cityId,
                        EventId = eventId,
                        CreatedAt = DateTime.UtcNow
                    }
                );

            return;
        }
    }
}
