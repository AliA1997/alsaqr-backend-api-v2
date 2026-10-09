namespace AlSaqr.Infrastructure
{
    /// <summary>
    /// Refuses to send a request over cleartext HTTP unless it targets this machine.
    /// Put it on any HttpClient that carries a server-side credential (for example the
    /// Supabase service-role key), so a misconfigured http:// URL fails loudly instead
    /// of leaking the credential on the network.
    /// </summary>
    public sealed class HttpsOrLoopbackHandler : DelegatingHandler
    {
        /// <summary>https anywhere, http only for loopback hosts (local development).</summary>
        public static bool IsAllowed(Uri uri) =>
            uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { IsAbsoluteUri: true } uri || !IsAllowed(uri))
            {
                throw new InvalidOperationException(
                    $"Refusing to send a request to {request.RequestUri?.Host}: https is required (http is allowed only for localhost).");
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
