namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityMemberRepository
    {
        Task JoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId);

        Task UnJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId);

        Task RequestJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId);

        Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            bool accept);
    }
}
