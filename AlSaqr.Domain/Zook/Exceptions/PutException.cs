using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Zook.Exceptions
{
    public class UpdateProductException : PutException
    {
        public Guid ProductId { get; }

        public UpdateProductException(Guid productId)
            : base($"Failed to update product with ID: {productId}.")
        {
            ProductId = productId;
        }

        public UpdateProductException(Guid productId, Exception innerException)
            : base($"Failed to update product with ID: {productId}.", innerException)
        {
            ProductId = productId;
        }
    }
}
