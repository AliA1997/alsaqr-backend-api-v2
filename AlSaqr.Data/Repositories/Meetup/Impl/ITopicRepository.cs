using AlSaqr.Data.Entities.Meetup;
using AlSaqr.Domain.Meetup;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface ITopicRepository
    {
        Task<Topics> InsertOrRetrieveTopic(Supabase.Client client, string name);
        Task InsertGroupTopics(Supabase.Client client, Guid groupId, string[] groupTopics);

        Task<List<TopicDto>> GetTopics(Supabase.Client client);
    }
}
