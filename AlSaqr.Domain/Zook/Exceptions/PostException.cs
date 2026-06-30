using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Zook.Exceptions
{
    public class CreateProductException : PostException
    {
        public string ProductName { get; }

        public CreateProductException(string productName)
            : base($"Failed to created product with name of: {productName}.")
        {
            ProductName = productName;
        }

        public CreateProductException(string productName, Exception innerException)
            : base($"Failed to created product with name of: {productName}.", innerException)
        {
           ProductName = productName;
        }
    }
}
