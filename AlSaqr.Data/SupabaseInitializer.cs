using Microsoft.Extensions.Hosting;

namespace AlSaqr.Data
{
    public class SupabaseInitializer : IHostedService
    {
        private readonly Supabase.Client _client;
        public SupabaseInitializer(Supabase.Client client) => _client = client;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _client.InitializeAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
