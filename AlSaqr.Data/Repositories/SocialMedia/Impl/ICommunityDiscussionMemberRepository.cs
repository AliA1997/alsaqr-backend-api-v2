
namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityDiscussionMemberRepository
    {
        Task JoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId);

        Task UnJoinCommunityDiscussion(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId);

        Task RequestJoinCommunityDiscussion(
              Supabase.Client supabase,
              Guid userId,
              Guid communityDiscussionId);

        Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityDiscussionId,
            bool accept);
    }
}
