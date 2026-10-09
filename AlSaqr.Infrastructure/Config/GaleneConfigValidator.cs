using System.Text;
using Microsoft.Extensions.Options;

namespace AlSaqr.Infrastructure.Config
{
    /// <summary>
    /// Fails startup (register with ValidateOnStart) on a Galène config that would
    /// break rooms at runtime or leak credentials and tokens over cleartext.
    /// </summary>
    public sealed class GaleneConfigValidator : IValidateOptions<GaleneConfig>
    {
        // Galène accepts HS256 room keys of exactly 32 bytes only.
        public const int TokenSecretBytes = 32;

        public ValidateOptionsResult Validate(string? name, GaleneConfig options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.AdminUsername))
                failures.Add("Galene:AdminUsername is required.");
            if (string.IsNullOrWhiteSpace(options.AdminPassword))
                failures.Add("Galene:AdminPassword is required.");
            if (string.IsNullOrEmpty(options.TokenSecret) || Encoding.UTF8.GetByteCount(options.TokenSecret) != TokenSecretBytes)
                failures.Add($"Galene:TokenSecret must be exactly {TokenSecretBytes} bytes.");

            CheckUrl(failures, "AdminUrl", options.AdminUrl);
            CheckUrl(failures, "PublicUrl", options.PublicUrl);

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        private static void CheckUrl(List<string> failures, string setting, string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                failures.Add($"Galene:{setting} must be an absolute URL.");
                return;
            }

            if (!HttpsOrLoopbackHandler.IsAllowed(uri))
                failures.Add($"Galene:{setting} must use https (http is allowed only for localhost).");
        }
    }
}
