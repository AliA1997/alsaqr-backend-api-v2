using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.CommunityDiscussion;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityDiscussionMessageRepository : ICommunityDiscussionMessageRepository
    {
        public CommunityDiscussionMessageRepository() { }

        public async Task<PaginatedResult<CommunityDiscussionMessageDto>> GetCommunityDiscussionMessages(
          Supabase.Client supabase,
          Guid communityDiscussionId,
          string? searchTerm,
          int currentPage,
          int itemsPerPage)
        {
            var communityDiscussionMessages = new List<CommunityDiscussionMessageDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var baseQuery = supabase.From<CommunityDiscussionMessage>().Where(x => x.CommunityDiscussionId == communityDiscussionId);
                var totalParams = new Dictionary<string, dynamic>()
                {
                    { "p_community_discussion_id", communityDiscussionId },
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    baseQuery = baseQuery.Where(x => x.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                    //baseQuery = baseQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_community_discussion_messages_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;

                if (totalItems == 0)
                {
                    return new PaginatedResult<CommunityDiscussionMessageDto>(
                        communityDiscussionMessages,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                communityDiscussionMessages = (await baseQuery.Order("created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwCommunityMsg => new CommunityDiscussionMessageDto(vwCommunityMsg))
                                .ToList();

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = (int)totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<CommunityDiscussionMessageDto>(communityDiscussionMessages, pagination!);
        }
    
        //public async Task<Guid> CreateCommunityDiscussionMessage
    }
}
