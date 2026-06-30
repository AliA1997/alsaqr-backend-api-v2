using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.SocialMedia.Exceptions
{
    public class JoinCommunityException : PatchException
    {
        public Guid CommunityId { get; }

        public JoinCommunityException(Guid communityId)
            : base($"Failed to join community: {communityId}.")
        {
            CommunityId = communityId;
        }

        public JoinCommunityException(Guid communityId, Exception innerException)
            : base($"Failed to join community with ID: {communityId}.", innerException)
        {
            CommunityId = communityId;
        }
    }

    public class UnJoinCommunityException : PatchException
    {
        public Guid CommunityId { get; }

        public UnJoinCommunityException(Guid communityId)
            : base($"Failed to unjoin community: {communityId}.")
        {
            CommunityId = communityId;
        }

        public UnJoinCommunityException(Guid communityId, Exception innerException)
            : base($"Failed to unjoin community with ID: {communityId}.", innerException)
        {
            CommunityId = communityId;
        }
    }

    public class RequestToJoinCommunityException : PatchException
    {
        public Guid CommunityId { get; }

        public RequestToJoinCommunityException(Guid communityId)
            : base($"Failed to request to join community: {communityId}.")
        {
            CommunityId = communityId;
        }

        public RequestToJoinCommunityException(Guid communityId, Exception innerException)
            : base($"Failed to request to join community with ID: {communityId}.", innerException)
        {
            CommunityId = communityId;
        }
    }

    public class RespondToRequestToJoinCommunityException : PatchException
    {
        public Guid CommunityId { get; }

        public RespondToRequestToJoinCommunityException(Guid communityId)
            : base($"Failed to respond to request to join community: {communityId}.")
        {
            CommunityId = communityId;
        }

        public RespondToRequestToJoinCommunityException(Guid communityId, Exception innerException)
            : base(
                $"Failed to respond to request to join community with ID: {communityId}.",
                innerException
            )
        {
            CommunityId = communityId;
        }
    }

    public class JoinCommunityDiscussionException : PatchException
    {
        public Guid CommunityDiscussionId { get; }

        public JoinCommunityDiscussionException(Guid communityDiscussionId)
            : base($"Failed to join community discussion: {communityDiscussionId}.")
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public JoinCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"Failed to join community discussion with ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class UnJoinCommunityDiscussionException : PatchException
    {
        public Guid CommunityDiscussionId { get; }

        public UnJoinCommunityDiscussionException(Guid communityDiscussionId)
            : base($"Failed to unjoin community discussion: {communityDiscussionId}.")
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public UnJoinCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"Failed to unjoin community discussion with ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class RequestToJoinCommunityDiscussionException : PatchException
    {
        public Guid CommunityDiscussionId { get; }

        public RequestToJoinCommunityDiscussionException(Guid communityDiscussionId)
            : base($"Failed to request to join community discussion: {communityDiscussionId}.")
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public RequestToJoinCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"Failed to request to join community discussion with ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class RespondToRequestToJoinCommunityDiscussionException : PatchException
    {
        public Guid CommunityDiscussionId { get; }

        public RespondToRequestToJoinCommunityDiscussionException(Guid communityDiscussionId)
            : base(
                $"Failed to respond to request to join community discussion: {communityDiscussionId}."
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }

        public RespondToRequestToJoinCommunityDiscussionException(
            Guid communityDiscussionId,
            Exception innerException
        )
            : base(
                $"Failed to respond to request to join community discussion with ID: {communityDiscussionId}.",
                innerException
            )
        {
            CommunityDiscussionId = communityDiscussionId;
        }
    }

    public class SavedItemToListException : PatchException
    {
        public Guid ListId { get; set; }
        public Guid EntityId { get; set; }

        public SavedItemToListException(Guid listId, Guid entityId)
            : base($"Failed to save item to list: {listId} with an entity with an ID: {entityId}.")
        {
            ListId = listId;
            EntityId = entityId;
        }

        public SavedItemToListException(Guid listId, Guid entityId, Exception innerException)
            : base(
                $"Failed to save item to list: {listId} with an entity with an ID: {entityId}.",
                innerException
            )
        {
            ListId = listId;
            EntityId = entityId;
        }
    }

    public class BookmarkPostException : PatchException
    {
        public Guid PostId { get; set; }

        public BookmarkPostException(Guid postId)
            : base($"Failed to bookmark post: {postId}.")
        {
            PostId = postId;
        }

        public BookmarkPostException(Guid postId, Exception innerException)
            : base($"Failed to bookmark post with ID: {postId}.", innerException)
        {
            PostId = postId;
        }
    }

    public class LikedPostException : PatchException
    {
        public Guid PostId { get; set; }

        public LikedPostException(Guid postId)
            : base($"Failed to like post: {postId}.")
        {
            PostId = postId;
        }

        public LikedPostException(Guid postId, Exception innerException)
            : base($"Failed to like post with ID: {postId}.", innerException)
        {
            PostId = postId;
        }
    }

    public class RepostPostException : PatchException
    {
        public Guid PostId { get; set; }

        public RepostPostException(Guid postId)
            : base($"Failed to repost post: {postId}.")
        {
            PostId = postId;
        }

        public RepostPostException(Guid postId, Exception innerException)
            : base($"Failed to repost post with ID: {postId}.", innerException)
        {
            PostId = postId;
        }
    }
}
