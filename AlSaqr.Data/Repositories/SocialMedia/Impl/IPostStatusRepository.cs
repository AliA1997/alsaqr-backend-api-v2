
namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IPostStatusRepository
    {
        Task BookmarkPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyBookmarked,
            CancellationToken ct);

        Task LikePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyLiked,
            CancellationToken ct);

        Task RepostPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyReposted,
            CancellationToken ct);
    }
}
