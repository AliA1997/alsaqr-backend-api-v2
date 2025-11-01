using AlSaqr.Data.Entities.Meetup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface ITopicRepository
    {
        Task<Topics> InsertOrRetrieveTopic(Supabase.Client client, string name);
        Task InsertGroupTopics(Supabase.Client client, int groupId, List<IDictionary<string, object>> groupTopics);
    }
}
