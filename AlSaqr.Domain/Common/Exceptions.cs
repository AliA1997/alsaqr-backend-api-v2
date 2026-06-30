
namespace AlSaqr.Domain.Common
{
    public class DeletionException : Exception
    {
        public DeletionException(string message)
            : base(message) { }

        public DeletionException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    
    public class PatchException : Exception
    {
        public PatchException(string message)
            : base(message) { }

        public PatchException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    
    public class PostException : Exception
    {
        public PostException(string message)
            : base(message) { }

        public PostException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    
    public class PutException : Exception
    {
        public PutException(string message)
            : base(message) { }

        public PutException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}