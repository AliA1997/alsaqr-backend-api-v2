
namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityDiscussionMemberRepository
    {
        Task JoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CancellationToken ct);

        Task UnJoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            CancellationToken ct);

        Task RequestJoinCommunityDiscussion(
              Supabase.Client supabase,
              Guid userId,
              Guid communityDiscussionId,
              CancellationToken ct);

        Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            bool accept,
            CancellationToken ct);
    }
}
