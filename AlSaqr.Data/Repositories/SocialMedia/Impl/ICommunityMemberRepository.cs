namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface ICommunityMemberRepository
    {
        Task JoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct);

        Task UnJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct);

        Task RequestJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct);


        Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            bool accept,
            CancellationToken ct);
    }
}
