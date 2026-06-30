using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.SocialMedia.Exceptions
{
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

    public class UpdateCommunityDiscussionException : PutException
    {
        public Guid CommunityDiscussionId { get; }

        public UpdateCommunityDiscussionException(Guid communityDiscussionId)
            : base($"Failed to update community discussion with ID: {communityDiscussionId}.")
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public UpdateCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"Failed to update community discussion with ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
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
