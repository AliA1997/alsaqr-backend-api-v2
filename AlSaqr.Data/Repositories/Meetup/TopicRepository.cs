using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using AlSaqr.Domain.Meetup;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class TopicRepository : ITopicRepository
    {
        public async Task<Topics> InsertOrRetrieveTopic(Supabase.Client client, string name)
        {
            Topics? topic = null;
            try
            {
                topic = (
                    await client.From<Topics>().Filter("name", Operator.Equals, name).Get()
                ).Model;
                if (topic == null)
                {
                    //var recentlyInsertedTopicId = await client.From<Topics>().Count(CountType.Estimated);
                    topic = (
                        await client
                            .From<Topics>()
                            .Upsert(
                                new Topics()
                                {
                                    Id = Guid.NewGuid(),
                                    Name = name,
                                    CreatedAt = DateTime.UtcNow,
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
                Console.WriteLine("Error getting topic in repository layer:", ex.Message);
            }

            return topic!;
        }

        public async Task InsertGroupTopics(
            Supabase.Client client,
            Guid groupId,
            string[] groupTopics
        )
        {
            foreach (var groupTopic in groupTopics)
            {
                var topic = await InsertOrRetrieveTopic(client, groupTopic);
                //var recentInsertedGroupTopic = await client.From<GroupTopics>().Count(CountType.Estimated);

                await client
                    .From<GroupTopics>()
                    .Upsert(
                        new GroupTopics()
                        {
                            Id = Guid.NewGuid(),
                            GroupId = groupId,
                            TopicId = topic.Id,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );
            }
        }

        public async Task<List<TopicDto>> GetTopics(Supabase.Client client)
        {
            // Reference data for a dropdown — no pagination. Deterministic ordering
            // (name, then the unique id as a tie-breaker) keeps the 100-row window stable.
            var response = await client
                .From<Topics>()
                .Where(c => c.Name != null)
                .Order("name", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get();

            // Distinct by name defensively (InsertOrRetrieveCity already keys on name),
            // then cap at 100 for the dropdown.
            return response.Models.DistinctBy(c => c.Name).Select(t => new TopicDto(t)).ToList();
        }
    }
}
