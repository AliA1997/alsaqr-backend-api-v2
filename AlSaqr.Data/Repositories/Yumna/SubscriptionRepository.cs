using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.Yumna;
using AlSaqr.Data.Repositories.Yumna.Impl;
using AlSaqr.Domain.Yumna.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.Yumna
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        public SubscriptionRepository() { }

        /// <summary>
        /// Returns the subscription the user is on via users.subscription_id,
        /// or null when the user has no subscription assigned.
        /// </summary>
        public async Task<Subscription?> GetUserSubscription(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default)
        {
            var user = await supabase
                .From<AlSaqrUser>()
                .Where(x => x.Id == userId)
                .Single(ct);

            if (user?.SubscriptionId == null || user.SubscriptionId == Guid.Empty)
                return null;

            return await supabase
                .From<Subscription>()
                .Where(x => x.Id == user.SubscriptionId)
                .Single(ct);
        }

        /// <summary>
        /// Returns the number of Yumna requests the user has made today (UTC).
        /// Zero when no row exists yet for today.
        /// </summary>
        public async Task<int> GetDailyUse(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default)
        {
            var dailyUse = await GetTodaysDailyUse(supabase, userId, ct);
            return dailyUse?.NumberOfRequests ?? 0;
        }

        /// <summary>
        /// Increments today's request count for the user, creating today's row on
        /// first use. Returns the updated count.
        /// </summary>
        public async Task<int> UpdateDailyUse(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default)
        {
            try
            {
                var existing = await GetTodaysDailyUse(supabase, userId, ct);

                if (existing == null)
                {
                    var dailyUse = new SubscriptionDailyUse
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Date = DateTime.UtcNow.Date,
                        NumberOfRequests = 1,
                        CreatedAt = DateTime.UtcNow
                    };

                    var inserted = await supabase
                        .From<SubscriptionDailyUse>()
                        .Insert(dailyUse, new QueryOptions
                        {
                            Returning = QueryOptions.ReturnType.Representation
                        }, ct);

                    if (inserted?.Model == null)
                        throw new UpdateSubscriptionDailyUseException(userId);

                    return inserted.Model.NumberOfRequests;
                }

                existing.NumberOfRequests += 1;
                existing.UpdatedAt = DateTime.UtcNow;

                var updated = await supabase
                    .From<SubscriptionDailyUse>()
                    .Upsert(existing, new QueryOptions
                    {
                        Returning = QueryOptions.ReturnType.Representation
                    }, ct);

                if (updated?.Model == null)
                    throw new UpdateSubscriptionDailyUseException(userId);

                return updated.Model.NumberOfRequests;
            }
            catch (UpdateSubscriptionDailyUseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UpdateSubscriptionDailyUseException(userId, ex);
            }
        }

        private static async Task<SubscriptionDailyUse?> GetTodaysDailyUse(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");

            return (await supabase
                .From<SubscriptionDailyUse>()
                .Where(x => x.UserId == userId)
                .Filter("date", Operator.Equals, today)
                .Get(ct)).Models.FirstOrDefault();
        }
    }
}
