namespace AlSaqr.Domain.SocialMedia.Exceptions
{
    public class PutException : Exception
    {
        public PutException(string message)
            : base(message) { }

        public PutException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class UpdateCommunityException : PutException
    {
        public Guid CommunityId { get; }
        public UpdateCommunityException(Guid communityId)
            : base($"Failed to update community with ID: {communityId}.")
        {
            CommunityId = communityId;
        }
        public UpdateCommunityException(Guid communityId, Exception innerException)
            : base($"Failed to update community with ID: {communityId}.", innerException)
        {
            CommunityId = communityId;
        }
    }

    public class UpdateUserException : PutException
    {
        public Guid UserId { get; }
        public UpdateUserException(Guid userId)
            : base($"Failed to update user with ID: {userId}.")
        {
            UserId = userId;
        }
        public UpdateUserException(Guid userId, Exception innerException)
            : base($"Failed to update user with ID: {userId}.", innerException)
        {
            UserId = userId;
        }
    }

}
