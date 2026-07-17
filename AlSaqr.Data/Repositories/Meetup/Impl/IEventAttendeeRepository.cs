namespace AlSaqr.Data.Repositories.Meetup.Impl
{
    public interface IEventAttendeeRepository
    {
        Task JoinEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct);

        Task LeaveEvent(
            Supabase.Client supabase,
            Guid userId,
            Guid eventId,
            CancellationToken ct);
        Task RemoveEventAttendee(
            Supabase.Client supabase,
            Guid founderId,
            Guid eventId,
            Guid attendeeUserId,
            CancellationToken ct);
    }
}
