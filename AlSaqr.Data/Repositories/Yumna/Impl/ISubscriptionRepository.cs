using AlSaqr.Data.Entities.Yumna;

namespace AlSaqr.Data.Repositories.Yumna.Impl
{
    public interface ISubscriptionRepository
    {
        Task<Subscription?> GetUserSubscription(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default);

        Task<int> GetDailyUse(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default);

        Task<int> UpdateDailyUse(
            Supabase.Client supabase,
            Guid userId,
            CancellationToken ct = default);
    }
}
