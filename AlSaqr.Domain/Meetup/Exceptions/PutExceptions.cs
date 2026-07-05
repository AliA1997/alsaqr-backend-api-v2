using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Meetup.Exceptions
{
    public class JoinGroupException : PutException
    {
        public Guid GroupId { get; }

        public JoinGroupException(Guid groupId)
            : base($"Failed to join group with ID: {groupId}.")
        {
            GroupId = groupId;
        }

        public JoinGroupException(Guid groupId, Exception innerException)
            : base($"Failed to join group with ID: {groupId}.", innerException)
        {
            GroupId = groupId;
        }
    }

    public class JoinEventException : PutException
    {
        public Guid EventId { get; }

        public JoinEventException(Guid eventId)
            : base($"Failed to join event with ID: {eventId}.")
        {
            EventId = eventId;
        }

        public JoinEventException(Guid eventId, Exception innerException)
            : base($"Failed to join event with ID: {eventId}.", innerException)
        {
            EventId = eventId;
        }
    }
}
