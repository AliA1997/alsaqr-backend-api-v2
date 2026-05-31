
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using static AlSaqr.Domain.SocialMedia.Messages;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class MessageRepository: IMessageRepository
    {
        public MessageRepository() { }

        public async Task<PaginatedResult<MessageDto>> GetMessages(
             Supabase.Client supabase,
             Guid userId,
             string? searchTerm,
             int currentPage,
             int itemsPerPage)
        {
            var messages = new List<MessageDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {

                var parameters = new Dictionary<string, dynamic>
                {
                    { "p_user_id", userId }
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    parameters.Add("p_search_term", searchTerm);
                }

                var countResult = await SupabaseHelper.CallFunction(supabase, "get_message_details_count", parameters);
                var totalItems = countResult != null ? long.Parse(countResult) : 0;
                if (totalItems == 0)
                {
                    return new PaginatedResult<MessageDto>(
                        messages,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }

                var dataQuery =  supabase
                                    .From<VwMessageDetails>()
                                    .Where(x => x.SenderId == userId || x.RecipientId == userId);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    dataQuery = dataQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var pageResult = await dataQuery
                    .Order(x => x.MessageCreatedAt, Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get();

                messages = pageResult.Models.Select(vwMsg => new MessageDto(vwMsg)).ToList();

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

            return new PaginatedResult<MessageDto>(messages, pagination!);

        }
    }
}
