using System.Net;

namespace AlSaqr.Tests.Support
{
    public sealed record RecordedRequest(
        HttpMethod Method, string Url, IReadOnlyDictionary<string, string> Headers, string Body);

    /// <summary>
    /// Stands in for an external HTTP API (Galène admin API, Supabase Realtime).
    /// Records every request and answers with <see cref="Respond"/> when set,
    /// otherwise with <see cref="Status"/>.
    /// </summary>
    public sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = new();

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public Func<RecordedRequest, HttpStatusCode>? Respond { get; set; }

        public string ResponseBody { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers
                .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            var body = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri!.ToString(), headers, body);
            Requests.Add(recorded);

            return new HttpResponseMessage(Respond?.Invoke(recorded) ?? Status)
            {
                Content = new StringContent(ResponseBody),
            };
        }
    }
}
