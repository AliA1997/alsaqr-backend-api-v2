namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IEventMemberRepository
    {
        Task JoinEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct);

        Task RemoveEventMember(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid memberUserId,
            CancellationToken ct);
    }
}
