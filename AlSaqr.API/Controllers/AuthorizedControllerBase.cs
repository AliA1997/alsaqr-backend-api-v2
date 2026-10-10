using AlSaqr.Domain.Utils;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AlSaqr.API.Controllers
{
    /// <summary>
    /// Base controller that centralizes the lightweight Bearer access-token gate
    /// (see specs/access-token.md) so it is not copy-pasted across controllers.
    /// Derive from this and call <see cref="ValidateAccessToken"/> at the top of any
    /// action that requires a valid (unexpired) access token.
    /// </summary>
    public abstract class AuthorizedControllerBase : ControllerBase
    {
        /// <summary>
        /// Validates the request's Authorization header. Returns an
        /// <see cref="UnauthorizedObjectResult"/> when the token is missing, malformed
        /// or expired; otherwise returns <c>null</c> so the action may proceed.
        /// </summary>
        /// <remarks>
        /// Performance: reads <c>Request.Headers.Authorization</c> directly (a typed
        /// <c>StringValues</c> accessor) rather than the string indexer, and the
        /// validator only base64-decodes the JWT payload — no signature work.
        /// </remarks>
        protected IActionResult? ValidateAccessToken()
        {
            if (!Auth.AccessTokenValidator.IsValid(Request.Headers.Authorization.ToString(), out var error))
                return Unauthorized(error);

            return null;
        }

        /// <summary>
        /// The expiry gate, then the caller id from the signature-verified token, for
        /// actions that must know who the caller really is. Identity never comes from
        /// the shared logged-in-user cache slot, which holds whoever logged in last.
        /// Returns an <see cref="UnauthorizedObjectResult"/> when either check fails.
        /// </summary>
        protected IActionResult? ValidateVerifiedCaller(IAccessTokenVerifier tokens, out Guid callerId)
        {
            callerId = Guid.Empty;

            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            callerId = tokens.GetVerifiedUserId(Request.Headers.Authorization.ToString());
            return callerId == Guid.Empty ? Unauthorized("A verified access token is required.") : null;
        }
    }
}
