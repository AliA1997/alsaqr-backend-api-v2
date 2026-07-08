namespace AlSaqr.Domain.Common
{
    /// <summary>
    /// Exceptions with a fixed HTTP mapping, thrown inside repositories and
    /// translated to RFC 7807 ProblemDetails by the global exception middleware
    /// (CLAUDE.md §3.3). The middleware is the single source of truth for the
    /// exception → status-code mapping: not-found → 404, validation → 400,
    /// forbidden → 403, conflict → 409.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }

        public NotFoundException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }

        public ValidationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) { }

        public ForbiddenException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }

        public ConflictException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
