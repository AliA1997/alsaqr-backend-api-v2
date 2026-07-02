using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Yumna.Exceptions
{
    public class UpdateSubscriptionDailyUseException : PutException
    {
        public Guid UserId { get; }

        public UpdateSubscriptionDailyUseException(Guid userId)
            : base($"Failed to update subscription daily use for user: {userId}.")
        {
            UserId = userId;
        }

        public UpdateSubscriptionDailyUseException(Guid userId, Exception innerException)
            : base($"Failed to update subscription daily use for user: {userId}.", innerException)
        {
            UserId = userId;
        }
    }
}
