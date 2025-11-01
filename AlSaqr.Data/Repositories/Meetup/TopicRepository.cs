using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Data.Repositories.Meetup.Impl;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Meetup
{
    public class TopicRepository: ITopicRepository
    {
        public async Task<Topics> InsertOrRetrieveTopic(Supabase.Client client, string name)
        {
            Topics? topic = null;
            try
            {
                topic = (await client.From<Topics>().Filter("name", Operator.Equals, name).Get()).Model;
                if (topic == null)
                {
                    var recentlyInsertedTopicId = await client.From<Topics>().Count(CountType.Estimated);
                    topic = (
                        await client.From<Topics>().Upsert(new Topics()
                        {
                            Id = recentlyInsertedTopicId + 1,
                            Name = name,
                            CreatedAt = DateTime.UtcNow
                        }, new QueryOptions() { Returning = QueryOptions.ReturnType.Representation })).Model;

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting topic in repository layer:", ex.Message);
            }

            return topic!;
        }
        public async Task InsertGroupTopics(Supabase.Client client, int groupId, List<IDictionary<string, object>> groupTopics)
        {
            foreach (var groupTopic in groupTopics)
            {
                var topic = await InsertOrRetrieveTopic(client, groupTopic["name"].ToString());
                var recentInsertedGroupTopic = await client.From<GroupTopics>().Count(CountType.Estimated);

                await client.From<GroupTopics>().Upsert(
                    new GroupTopics()
                    {
                        Id = recentInsertedGroupTopic + 1,
                        GroupId = groupId,
                        TopicId = topic.Id,
                        CreatedAt = DateTime.UtcNow
                    }
                );
            }
        }
    }
}
