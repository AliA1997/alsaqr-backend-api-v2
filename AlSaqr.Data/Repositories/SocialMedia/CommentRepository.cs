
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;
using Notification = AlSaqr.Data.Entities.SocialMedia.Notification;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommentRepository : ICommentRepository
    {
        public CommentRepository() { }

        public async Task<PaginatedResult<PostDto>> GetComments(
            Supabase.Client supabase,
            Guid postId,
            int currentPage,
            int itemsPerPage)
        {
            var postComments = new List<PostDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;


            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var baseQuery = supabase.From<VwPostDetails>().Where(x => x.RelatedPostId == postId && x.PostType == "comment");
                var totalParams = new Dictionary<string, dynamic>()
                {
                    { "p_post_id", postId },
                };


                var result = await SupabaseHelper.CallFunction(supabase, "get_all_comments_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<PostDto>(
                        postComments,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                postComments = (await baseQuery.Order("post_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwPost => new PostDto(vwPost))
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

            return new PaginatedResult<PostDto>(postComments, pagination!);
        }

        public async Task<Guid> CreateComment(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            Posts.CreateCommentDto data,
            CancellationToken ct)
        {
            try
            {
                var comment = new Post
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = data.Text,
                    BannerImage = data.Image ?? string.Empty,
                    PostType = "comment",
                    Tags = Array.Empty<string>(),
                    RelatedPostId = postId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                var inserted = await supabase
                                .From<Post>()
                                .Insert(comment, new QueryOptions
                                {
                                    Returning = ReturnType.Representation
                                }, ct);

                if (inserted?.Model == null)
                    throw new Exception("Error creating post");

                await CreateNotificationAfterUpsert(
                    supabase,
                    originalPostId: postId,
                    commentId: inserted.Model.Id,
                    userId,
                    "Post commented by",
                    "comment_on_post",
                    ct);

                return inserted.Model.Id;
            }
            catch(CreateCommentException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CreateCommentException(postId, ex);
            }
 
        }

        private async Task CreateNotificationAfterUpsert(
              Supabase.Client supabase,
              Guid originalPostId,
              Guid commentId,
              Guid userId,
              string notificationMsgPrefix,
              string notificationType,
              CancellationToken ct)
        {
            if (originalPostId != Guid.Empty)
            {
                var originalPost = await supabase.From<Post>().Where(p => p.Id == originalPostId).Single(ct);

                if(originalPost == null)
                {
                    throw new Exception($"Commenting Original post with ID {originalPostId} not found");
                }

                var repostingUser = await supabase
                   .From<AlSaqrUser>()
                   .Where(u => u.Id == userId)
                   .Single(ct);

                var username = repostingUser?.Username ?? "Someone";

                var notification = new Notification()
                {
                    Id = Guid.NewGuid(),
                    UserId = originalPost.UserId,
                    Read = false,
                    CreatedAt = DateTime.UtcNow,
                    Message = $"{notificationMsgPrefix} {username}",
                    NotificationType = notificationType,
                    ItemType = "comment",
                    PostId = commentId,
                    RelatedUserId = userId,
                    Link = $"/status/{commentId}"
                };


                var newNotification = await supabase.From<Notification>().Insert(notification, new QueryOptions()
                {
                    Returning = ReturnType.Representation

                }, ct);

                if (newNotification == null)
                {
                    throw new Exception($"Error creating notification");
                }
            }

            return;
        }
    }
}
