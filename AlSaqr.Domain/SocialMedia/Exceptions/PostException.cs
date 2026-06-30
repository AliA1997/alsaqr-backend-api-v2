using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.SocialMedia.Exceptions
{
    public class CompleteRegistrationException : PostException
    {
        public Guid UserId { get; }

        public CompleteRegistrationException(Guid userId)
            : base($"Failed to complete registration for user with an ID: {userId}.")
        {
            UserId = userId;
        }

        public CompleteRegistrationException(Guid userId, Exception innerException)
            : base(
                $"Failed to complete registration for user with an ID: {userId}.",
                innerException
            )
        {
            UserId = userId;
        }
    }

    public class AddUserFollowException : PostException
    {
        public Guid UserId { get; }
        public Guid FollowedUserId { get; }

        public AddUserFollowException(Guid userId, Guid followedUserId)
            : base(
                $"Failed to add follow from user with ID: {userId} to user with ID: {followedUserId}."
            )
        {
            UserId = userId;
            FollowedUserId = followedUserId;
        }

        public AddUserFollowException(Guid userId, Guid followedUserId, Exception innerException)
            : base(
                $"Failed to add follow from user with ID: {userId} to user with ID: {followedUserId}.",
                innerException
            )
        {
            UserId = userId;
            FollowedUserId = followedUserId;
        }
    }

    public class RemoveUserFollowException : PostException
    {
        public Guid UserId { get; }
        public Guid UnfollowedUserId { get; }

        public RemoveUserFollowException(Guid userId, Guid unfollowedUserId)
            : base(
                $"Failed to remove follow from user with ID: {userId} to user with ID: {unfollowedUserId}."
            )
        {
            UserId = userId;
            UnfollowedUserId = unfollowedUserId;
        }

        public RemoveUserFollowException(
            Guid userId,
            Guid unfollowedUserId,
            Exception innerException
        )
            : base(
                $"Failed to remove follow from user with ID: {userId} to user with ID: {unfollowedUserId}.",
                innerException
            )
        {
            UserId = userId;
            UnfollowedUserId = unfollowedUserId;
        }
    }

    public class CreateCommunityException : PostException
    {
        public Guid UserId { get; }

        public CreateCommunityException(Guid userId)
            : base($"Failed to create community with a user with ID: {userId}.")
        {
            UserId = userId;
        }

        public CreateCommunityException(Guid userId, Exception innerException)
            : base($"Failed to create community with a user with ID: {userId}.", innerException)
        {
            UserId = userId;
        }
    }

    public class CreateRequestToJoinCommunityException : PostException
    {
        public Guid CommunityId { get; }

        public CreateRequestToJoinCommunityException(Guid communityId)
            : base($"Failed to create request to join community with ID: {communityId}.")
        {
            CommunityId = communityId;
        }

        public CreateRequestToJoinCommunityException(Guid communityId, Exception innerException)
            : base(
                $"Failed to create request to join community with ID: {communityId}.",
                innerException
            )
        {
            CommunityId = communityId;
        }
    }

    public class CreateCommunityDiscussionException : PostException
    {
        public Guid CommunityId { get; }

        public CreateCommunityDiscussionException(Guid communityId)
            : base($"Failed to create community discussion with a community ID: {communityId}.")
        {
            CommunityId = communityId;
        }

        public CreateCommunityDiscussionException(Guid communityId, Exception innerException)
            : base(
                $"Failed to create community with a community ID: {communityId}.",
                innerException
            )
        {
            CommunityId = communityId;
        }
    }

    public class CreateRequestToJoinCommunityDiscussionException : PostException
    {
        public Guid CommunityDiscussionId { get; }

        public CreateRequestToJoinCommunityDiscussionException(Guid communityDiscussionId)
            : base(
                $"Failed to create request to join community discussion with a ID: {communityDiscussionId}."
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public CreateRequestToJoinCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"\"Failed to create request to join community discussion with a ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class CreateCommunityDiscussionMessageException : PostException
    {
        public Guid CommunityDiscussionId { get; }

        public CreateCommunityDiscussionMessageException(Guid communityDiscussionId)
            : base(
                $"Failed to create community discussion message for a community discussion with a ID: {communityDiscussionId}."
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public CreateCommunityDiscussionMessageException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"\"Failed to create community discussion message for a community discussion with a ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class CreateMessageException : PostException
    {
        public Guid SenderId { get; }
        public Guid RecipientId { get; }

        public CreateMessageException(Guid senderId, Guid recipientId)
            : base(
                $"Failed to create message from user with ID: {senderId} to user with ID: {recipientId}."
            )
        {
            SenderId = senderId;
            RecipientId = recipientId;
        }

        public CreateMessageException(Guid senderId, Guid recipientId, Exception innerException)
            : base(
                $"Failed to create message from user with ID: {senderId} to user with ID: {recipientId}.",
                innerException
            )
        {
            SenderId = senderId;
            RecipientId = recipientId;
        }
    }

    public class CreateListException : PostException
    {
        public Guid UserId { get; }
        public string ListName { get; }

        public CreateListException(Guid userId, string listName)
            : base($"Failed to create list with name: {listName} for user with ID: {userId}.")
        {
            UserId = userId;
            ListName = listName;
        }

        public CreateListException(Guid userId, string listName, Exception innerException)
            : base(
                $"Failed to create list with name: {listName} for user with ID: {userId}.",
                innerException
            )
        {
            UserId = userId;
            ListName = listName;
        }
    }

    public class CreateCommentException : PostException
    {
        public Guid PostId { get; }

        public CreateCommentException(Guid postId)
            : base($"Failed to create comment for post with ID: {postId}.")
        {
            PostId = postId;
        }

        public CreateCommentException(Guid postId, Exception innerException)
            : base($"Failed to create comment for post with ID: {postId}.", innerException)
        {
            PostId = postId;
        }
    }

    public class CreatePostException : PostException
    {
        public Guid UserId { get; }

        public CreatePostException(Guid userId)
            : base($"Failed to create post with a user ID: {userId}.")
        {
            UserId = userId;
        }

        public CreatePostException(Guid userId, Exception innerException)
            : base($"Failed to create post with a user ID: {userId}.", innerException)
        {
            UserId = userId;
        }
    }
}
