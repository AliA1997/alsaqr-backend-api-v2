
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class PostStatusRepository : IPostStatusRepository
    {
        public PostStatusRepository() { }

        public async Task BookmarkPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyBookmarked)
        {
            if (!currentlyBookmarked)
            {
                // ── ADD bookmark ────────────────────────────────────────────────
                // Upsert a PostStatus row with action = "bookmarked"
                var status = new PostStatus
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PostId = postId,
                    Action = "bookmarked",
                    CreatedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<PostStatus>()
                    .Upsert(status, new QueryOptions
                    {
                        Returning = ReturnType.Minimal
                    });

                // Resolve the post author so the notification lands on the right user
                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();


                await CreateNotificationAfterUpsert(
                    supabase,
                    userId: post.UserId,
                    post: post,
                    notificationMsgPrefix: "Post bookmarked by",
                    notificationType: "bookmarked_post"
                );

            }
            else
            {
                // ── REMOVE bookmark ─────────────────────────────────────────────
                await supabase
                    .From<PostStatus>()
                    .Where(ps => ps.UserId == userId && ps.PostId == postId)
                    .Filter("action", Operator.Equals, "bookmarked")
                    .Delete();

                // Delete the matching notification from the post author's feed
                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();

                if (post != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == post.UserId
                                 && n.PostId == postId
                                 && n.NotificationType == "bookmarked_post")
                        .Delete();
                }
            }
        }

        public async Task LikePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyLiked)
        {
            if (!currentlyLiked)
            {
                // ── ADD like ─────────────────────────────────────────────────────
                var status = new PostStatus
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PostId = postId,
                    Action = "liked",
                    CreatedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<PostStatus>()
                    .Upsert(status, new QueryOptions
                    {
                        Returning = ReturnType.Minimal
                    });

                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();

                await CreateNotificationAfterUpsert(
                    supabase,
                    userId: post.UserId,
                    post: post,
                    notificationMsgPrefix: "Post liked by",
                    notificationType: "liked_post"
                );

            }
            else
            {
                // ── REMOVE like ──────────────────────────────────────────────────
                await supabase
                    .From<PostStatus>()
                    .Where(ps => ps.UserId == userId && ps.PostId == postId)
                    .Filter("action", Operator.Equals, "liked")
                    .Delete();

                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();

                if (post != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == post.UserId
                                 && n.PostId == postId
                                 && n.NotificationType == "liked_post")
                        .Delete();
                }
            }
        }

        public async Task RepostPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyReposted)
        {
            if (!currentlyReposted)
            {
                // ── ADD repost ───────────────────────────────────────────────────
                var status = new PostStatus
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PostId = postId,
                    Action = "reposted",
                    CreatedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<PostStatus>()
                    .Upsert(status, new QueryOptions
                    {
                        Returning = ReturnType.Minimal
                    });

                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();

                await CreateNotificationAfterUpsert(
                    supabase,
                    userId: post.UserId,
                    post:post,
                    notificationMsgPrefix: "Post reposted by",
                    notificationType: "reposted_post"
                );
            }
            else
            {
                // ── REMOVE repost ────────────────────────────────────────────────
                await supabase
                    .From<PostStatus>()
                    .Where(ps => ps.UserId == userId && ps.PostId == postId)
                    .Filter("action", Operator.Equals, "reposted")
                    .Delete();

                var post = await supabase
                    .From<Post>()
                    .Where(p => p.Id == postId)
                    .Single();

                if (post != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == post.UserId
                                 && n.PostId == postId
                                 && n.NotificationType == "reposted_post")
                        .Delete();
                }
            }
        }

       private async Task CreateNotificationAfterUpsert(
         Supabase.Client supabase,
         Post? post,
         Guid userId,
         string notificationMsgPrefix,
         string notificationType)
        {
            if (post != null && post.UserId != userId)
            {
                var repostingUser = await supabase
                   .From<AlSaqrUser>()
                   .Where(u => u.Id == userId)
                   .Single();

                var username = repostingUser?.Username ?? "Someone";

                var notification = new Notification()
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Read = false,
                    CreatedAt = DateTime.UtcNow,
                    Message = $"{notificationMsgPrefix} {username}",
                    NotificationType = notificationType,
                    ItemType = "post",
                    PostId = post.Id,
                    Link = $"/status/{post.Id}"
                };


                var newNotification = await supabase.From<Notification>().Insert(notification, new QueryOptions()
                {
                    Returning = ReturnType.Minimal

                });

                if (newNotification == null)
                {
                    throw new Exception($"Error creating notification");
                }
            }

            return;
        }
    }
}
