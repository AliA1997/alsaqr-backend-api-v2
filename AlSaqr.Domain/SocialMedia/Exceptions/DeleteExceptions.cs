using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.SocialMedia.Exceptions
{
    public class DeletePostException : DeletionException
    {
        public Guid PostId { get; }

        public DeletePostException(Guid postId)
            : base($"Failed to delete post with ID: {postId}.")
        {
            PostId = postId;
        }

        public DeletePostException(Guid postId, Exception innerException)
            : base($"Failed to delete post with ID: {postId}.", innerException)
        {
            PostId = postId;
        }
    }

    public class DeletePostStatusException : DeletionException
    {
        public Guid PostStatusId { get; }

        public DeletePostStatusException(Guid postStatusId)
            : base($"Failed to delete post status with ID: {postStatusId}.")
        {
            PostStatusId = postStatusId;
        }

        public DeletePostStatusException(Guid postStatusId, Exception innerException)
            : base($"Failed to delete post status with ID: {postStatusId}.", innerException)
        {
            PostStatusId = postStatusId;
        }
    }

    public class DeleteListException : DeletionException
    {
        public Guid ListId { get; }

        public DeleteListException(Guid listId)
            : base($"Failed to delete list with ID: {listId}.")
        {
            ListId = listId;
        }

        public DeleteListException(Guid listId, Exception innerException)
            : base($"Failed to delete list with ID: {listId}.", innerException)
        {
            ListId = listId;
        }
    }

    public class DeleteListItemException : DeletionException
    {
        public Guid ListItemId { get; }

        public DeleteListItemException(Guid listItemId)
            : base($"Failed to delete list item with ID: {listItemId}.")
        {
            ListItemId = listItemId;
        }

        public DeleteListItemException(Guid listItemId, Exception innerException)
            : base($"Failed to delete list item with ID: {listItemId}.", innerException)
        {
            ListItemId = listItemId;
        }
    }

    public class DeleteNotificationException : DeletionException
    {
        public Guid NotificationId { get; }

        public DeleteNotificationException(Guid notificationId)
            : base($"Failed to delete notification with ID: {notificationId}.")
        {
            NotificationId = notificationId;
        }

        public DeleteNotificationException(Guid notificationId, Exception innerException)
            : base($"Failed to delete notification with ID: {notificationId}.", innerException)
        {
            NotificationId = notificationId;
        }
    }

    public class DeleteDirectMessageException : DeletionException
    {
        public Guid DirectMessageId { get; }

        public DeleteDirectMessageException(Guid directMessageId)
            : base($"Failed to delete direct message with ID: {directMessageId}.")
        {
            DirectMessageId = directMessageId;
        }

        public DeleteDirectMessageException(Guid directMessageId, Exception innerException)
            : base($"Failed to delete direct message with ID: {directMessageId}.", innerException)
        {
            DirectMessageId = directMessageId;
        }
    }

    public class DeleteCommunityDiscussionMessageException : DeletionException
    {
        public Guid DiscussionMessageId { get; }

        public DeleteCommunityDiscussionMessageException(Guid discussionMessageId)
            : base($"Failed to delete community discussion message with ID: {discussionMessageId}.")
        {
            DiscussionMessageId = discussionMessageId;
        }

        public DeleteCommunityDiscussionMessageException(
            Guid discussionMessageId,
            Exception innerException
        )
            : base(
                $"Failed to delete community discussion message with ID: {discussionMessageId}.",
                innerException
            )
        {
            DiscussionMessageId = discussionMessageId;
        }
    }

    public class DeleteCommunityDiscussionMemberException : DeletionException
    {
        public Guid UserId { get; set; }
        public Guid CommunityId { get; set; }
        public Guid CommunityDiscussionId { get; set; }
        public Guid CommunityDiscussionMemberId { get; }

        public DeleteCommunityDiscussionMemberException(
            Guid communityDiscussionMemberId,
            Guid communityId,
            Guid communityDiscussionId,
            Guid userId
        )
            : base(
                @$"
                        Failed to delete community discussion member with ID: {communityDiscussionMemberId}.
                        user with ID: {userId}.
                        community with ID: {communityId}
                        community discussion with ID: {communityDiscussionId}
            "
            )
        {
            UserId = userId;
            CommunityDiscussionId = communityDiscussionId;
            CommunityId = communityId;
            CommunityDiscussionMemberId = communityDiscussionMemberId;
        }

        public DeleteCommunityDiscussionMemberException(
            Guid communityDiscussionMemberId,
            Guid communityId,
            Guid communityDiscussionId,
            Guid userId,
            Exception innerException
        )
            : base(
                @$"
                Failed to delete community discussion member with ID: {communityDiscussionMemberId}.\n 
                user with ID: {userId}.
                community with ID: {communityId}
                community discussion with ID: {communityDiscussionId}
            ",
                innerException
            )
        {
            UserId = userId;
            CommunityDiscussionId = communityDiscussionId;
            CommunityId = communityId;
            CommunityDiscussionMemberId = communityDiscussionMemberId;
        }
    }

    public class DeleteCommunityException : DeletionException
    {
        public Guid UserId { get; set; }
        public Guid CommunityId { get; set; }

        public DeleteCommunityException(Guid communityId, Guid userId)
            : base(
                @$"
                        Failed to delete community with ID: {communityId}.
                        user with ID: {userId}.
            "
            )
        {
            UserId = userId;
            CommunityId = communityId;
        }

        public DeleteCommunityException(Guid communityId, Guid userId, Exception innerException)
            : base(
                @$"
                        Failed to delete community with ID: {communityId}.
                        user with ID: {userId}.
            ",
                innerException
            )
        {
            UserId = userId;
            CommunityId = communityId;
        }
    }

    public class DeleteCommunityDiscussionException : DeletionException
    {
        public Guid UserId { get; set; }
        public Guid CommunityDiscussionId { get; set; }

        public DeleteCommunityDiscussionException(Guid communityDiscussionId, Guid userId)
            : base(
                @$"
                        Failed to delete community discussion with ID: {communityDiscussionId}.
                        user with ID: {userId}.
            "
            )
        {
            UserId = userId;
            CommunityDiscussionId = communityDiscussionId;
        }

        public DeleteCommunityDiscussionException(
            Guid communityDiscussionId,
            Guid userId,
            Exception innerException
        )
            : base(
                @$"
                        Failed to delete community discussion with ID: {communityDiscussionId}.
                        user with ID: {userId}.
            ",
                innerException
            )
        {
            UserId = userId;
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class DeleteCommunityMemberException : DeletionException
    {
        public Guid UserId { get; set; }
        public Guid CommunityId { get; set; }
        public Guid CommunityMemberId { get; }

        public DeleteCommunityMemberException(Guid communityMemberId, Guid communityId, Guid userId)
            : base(
                @$"
                        Failed to delete community member with ID: {communityMemberId}.
                        user with ID: {userId}.
                        community with ID: {communityId}
            "
            )
        {
            UserId = userId;
            CommunityId = communityId;
            CommunityMemberId = communityMemberId;
        }

        public DeleteCommunityMemberException(
            Guid communityMemberId,
            Guid communityId,
            Guid userId,
            Exception innerException
        )
            : base(
                @$"
                    Failed to delete community member with ID: {communityMemberId}.
                    user with ID: {userId}.
                    community with ID: {communityId}
            ",
                innerException
            )
        {
            UserId = userId;
            CommunityId = communityId;
            CommunityMemberId = communityMemberId;
        }
    }
}
