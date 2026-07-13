using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Meetup.Exceptions
{
    public class CreateGroupException : PutException
    {
        public Guid UserId { get; }
        public string GroupName { get; }

        public CreateGroupException(Guid userId, string groupName)
            : base($"Failed to create group with name of: {groupName}, by a user with an id: {userId}.")
        {
            UserId = userId;
            GroupName = groupName;
        }

        public CreateGroupException(Guid userId, string groupName, Exception innerException)
            : base($"Failed to create group with name of: {groupName}, by a user with an id: {userId}.", innerException)
        {
            UserId = userId;
            GroupName = groupName;
        }
    }

    
    public class CreateEventException : PutException
    {
        public Guid UserId { get; }
        public string EventName { get; }

        public CreateEventException(Guid userId, string eventName)
            : base($"Failed to create event with name of: {eventName}, by a user with an id: {userId}.")
        {
            UserId = userId;
            EventName = eventName;
        }

        public CreateEventException(Guid userId, string eventName, Exception innerException)
            : base($"Failed to create event with name of: {eventName}, by a user with an id: {userId}.", innerException)
        {
            UserId = userId;
            EventName = eventName;
        }
    }
}