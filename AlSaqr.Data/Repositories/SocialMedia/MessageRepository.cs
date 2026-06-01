
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using Supabase.Postgrest;
using static AlSaqr.Domain.SocialMedia.Messages;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

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


        public async Task<Guid> SendMessage(
              Supabase.Client supabase,
              Guid userId,
              Messages.MessageFormDto data)
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SenderId = data.SenderId,
                RecipientId = data.RecipientId,
                Content = data.Text,
                Image = data.Image,
                CreatedAt = DateTime.UtcNow
            };

            var inserted = await supabase
                .From<Message>()
                .Insert(message, new QueryOptions
                {
                    Returning = ReturnType.Representation
                });

            if (inserted?.Model == null)
                throw new Exception("Error creating message");

            await CreateMessageNotification(
                supabase,
                data.SenderId,
                data.SenderUsername,
                data.RecipientId,
                data.RecipientUsername,
                ct);

            return inserted.Model.Id;
        }

        private async Task CreateMessageNotification(
            Supabase.Client supabase,
            Guid senderId,
            string senderUsername,
            Guid recipientId,
            string recipientUsername,
            CancellationToken ct)
        {
            var senderMessage = await supabase
                .From<Message>()
                .Where(m => m.Id == senderId)
                .Single(ct);
            var recipientMessage = await supabase
                .From<Message>()
                .Where(m => m.Id == recipientId)
                .Single(ct);

            if (senderMessage == null || recipientMessage == null)
                return;


            var senderNotificationMsg = $"You sent a message to {recipientUsername}";
            var recipientNotificationMsg = $"{senderUsername} sent a message to you";


            var senderNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = senderId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = senderNotificationMsg,
                NotificationType = "sent_message",
                ItemType = "user",
                RelatedUserId = recipientId,
                Link = $"/users/{recipientUsername}",
            };


            var recipientNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipientId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = recipientNotificationMsg,
                NotificationType = "received_message",
                ItemType = "user",
                RelatedUserId = senderId,
                Link = $"/users/{senderUsername}",
            };

            var createdSender = await supabase
                .From<Notification>()
                .Insert(senderNotification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (createdSender == null)
                throw new Exception("Error creating notification");

            var createdRecipient = await supabase
                .From<Notification>()
                .Insert(recipientNotification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (createdRecipient == null)
                throw new Exception("Error creating notification");

            return;
        }
    }
}
