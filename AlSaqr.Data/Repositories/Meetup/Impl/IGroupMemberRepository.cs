namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IGroupMemberRepository
    {
        Task JoinGroup(
            Supabase.Client supabase,
            Guid userId,
            Guid groupId,
            CancellationToken ct);

        Task RemoveGroupMember(
            Supabase.Client supabase,
            Guid founderId,
            Guid groupId,
            Guid memberUserId,
            CancellationToken ct);
    }
}
