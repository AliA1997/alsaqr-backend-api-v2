
namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IPostStatusRepository
    {
        Task BookmarkPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyBookmarked);

        Task LikePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyLiked);

        Task RepostPost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId,
            bool currentlyReposted);
    }
}
