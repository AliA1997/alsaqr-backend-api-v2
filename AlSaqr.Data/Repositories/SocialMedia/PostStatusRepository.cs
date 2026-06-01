
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
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
            bool currentlyBookmarked,
            CancellationToken ct)
        {
            try 
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
                        }, ct);

                    // Resolve the post author so the notification lands on the right user
                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);


                    await CreateNotificationAfterUpsert(
                        supabase,
                        userId: post.UserId,
                        post: post,
                        notificationMsgPrefix: "Post bookmarked by",
                        notificationType: "bookmarked_post",
                        ct: ct
                    );

                }
                else
                {
                    // ── REMOVE bookmark ─────────────────────────────────────────────
                    await supabase
                        .From<PostStatus>()
                        .Where(ps => ps.UserId == userId && ps.PostId == postId)
                        .Filter("action", Operator.Equals, "bookmarked")
                        .Delete(null, ct);

                    // Delete the matching notification from the post author's feed
                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);

                    if (post != null)
                    {
                        await supabase
                            .From<Notification>()
                            .Where(n => n.UserId == post.UserId
                                     && n.PostId == postId
                                     && n.NotificationType == "bookmarked_post")
                            .Delete(null, ct);
                    }
                }
            }
            catch(BookmarkPostException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new BookmarkPostException(postId, ex);
            }
        }

        public async Task LikePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyLiked,
            CancellationToken ct)
        {
            try 
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
                        }, ct);

                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);

                    await CreateNotificationAfterUpsert(
                        supabase,
                        userId: post.UserId,
                        post: post,
                        notificationMsgPrefix: "Post liked by",
                        notificationType: "liked_post",
                        ct: ct
                    );

                }
                else
                {
                    // ── REMOVE like ──────────────────────────────────────────────────
                    await supabase
                        .From<PostStatus>()
                        .Where(ps => ps.UserId == userId && ps.PostId == postId)
                        .Filter("action", Operator.Equals, "liked")
                        .Delete(null, ct);

                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);

                    if (post != null)
                    {
                        await supabase
                            .From<Notification>()
                            .Where(n => n.UserId == post.UserId
                                     && n.PostId == postId
                                     && n.NotificationType == "liked_post")
                            .Delete(null, ct);
                    }
                }
            }
            catch(LikedPostException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new LikedPostException(postId, ex);
            }


        }

        public async Task RepostPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyReposted,
            CancellationToken ct)
        {
            try 
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
                        }, ct);

                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);

                    await CreateNotificationAfterUpsert(
                        supabase,
                        userId: post.UserId,
                        post: post,
                        notificationMsgPrefix: "Post reposted by",
                        notificationType: "reposted_post",
                        ct: ct
                    );
                }
                else
                {
                    // ── REMOVE repost ────────────────────────────────────────────────
                    await supabase
                        .From<PostStatus>()
                        .Where(ps => ps.UserId == userId && ps.PostId == postId)
                        .Filter("action", Operator.Equals, "reposted")
                        .Delete(null, ct);

                    var post = await supabase
                        .From<Post>()
                        .Where(p => p.Id == postId)
                        .Single(ct);

                    if (post != null)
                    {
                        await supabase
                            .From<Notification>()
                            .Where(n => n.UserId == post.UserId
                                     && n.PostId == postId
                                     && n.NotificationType == "reposted_post")
                            .Delete(null, ct);
                    }
                }
            }
            catch(RepostPostException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RepostPostException(postId, ex);
            }
 
        }

       private async Task CreateNotificationAfterUpsert(
         Supabase.Client supabase,
         Post? post,
         Guid userId,
         string notificationMsgPrefix,
         string notificationType,
         CancellationToken ct)
        {
            if (post != null && post.UserId != userId)
            {
                var repostingUser = await supabase
                   .From<AlSaqrUser>()
                   .Where(u => u.Id == userId)
                   .Single(ct);

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
