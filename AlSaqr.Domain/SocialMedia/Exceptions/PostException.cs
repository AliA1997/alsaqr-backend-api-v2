using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.SocialMedia.Exceptions
{
    public class PostException : Exception
    {
        public PostException(string message)
            : base(message) { }

        public PostException(string message, Exception innerException)
            : base(message, innerException) { }
    }


    public class AddUserFollowException : PostException
    {
        public Guid UserId { get; }
        public Guid FollowedUserId { get; }
        public AddUserFollowException(Guid userId, Guid followedUserId)
            : base($"Failed to add follow from user with ID: {userId} to user with ID: {followedUserId}.")
        {
            UserId = userId;
            FollowedUserId = followedUserId;
        }
        public AddUserFollowException(Guid userId, Guid followedUserId, Exception innerException)
            : base($"Failed to add follow from user with ID: {userId} to user with ID: {followedUserId}.", innerException)
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
            : base($"Failed to remove follow from user with ID: {userId} to user with ID: {unfollowedUserId}.")
        {
            UserId = userId;
            UnfollowedUserId = unfollowedUserId;
        }
        public RemoveUserFollowException(Guid userId, Guid unfollowedUserId, Exception innerException)
            : base($"Failed to remove follow from user with ID: {userId} to user with ID: {unfollowedUserId}.", innerException)
        {
            UserId = userId;
            UnfollowedUserId = unfollowedUserId;
        }
    }


    public class CreateMessageException : PostException
    {
        public Guid SenderId { get; }
        public Guid RecipientId { get; }
        public CreateMessageException(Guid senderId, Guid recipientId)
            : base($"Failed to create message from user with ID: {senderId} to user with ID: {recipientId}.")
        {
            SenderId = senderId;
            RecipientId = recipientId;
        }
        public CreateMessageException(Guid senderId, Guid recipientId, Exception innerException)
            : base($"Failed to create message from user with ID: {senderId} to user with ID: {recipientId}.", innerException)
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
            : base($"Failed to create list with name: {listName} for user with ID: {userId}.", innerException)
        {
            UserId = userId;
            ListName = listName;
        }
    }
}