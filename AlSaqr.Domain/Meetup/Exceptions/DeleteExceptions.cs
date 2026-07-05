using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Meetup.Exceptions
{
    public class UnjoinGroupException : DeletionException
    {
        public Guid GroupId { get; }

        public UnjoinGroupException(Guid groupId)
            : base($"Failed to remove member from group with ID: {groupId}.")
        {
            GroupId = groupId;
        }

        public UnjoinGroupException(Guid groupId, Exception innerException)
            : base($"Failed to remove member from group with ID: {groupId}.", innerException)
        {
            GroupId = groupId;
        }
    }

    public class UnjoinEventException : DeletionException
    {
        public Guid EventId { get; }

        public UnjoinEventException(Guid eventId)
            : base($"Failed to remove member from event with ID: {eventId}.")
        {
            EventId = eventId;
        }

        public UnjoinEventException(Guid eventId, Exception innerException)
            : base($"Failed to remove member from event with ID: {eventId}.", innerException)
        {
            EventId = eventId;
        }
    }
}
