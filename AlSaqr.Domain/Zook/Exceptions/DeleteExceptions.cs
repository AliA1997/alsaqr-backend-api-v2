using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Zook.Exceptions
{
    public class DeleteProductException : PostException
    {
        public Guid ProductId { get; }

        public DeleteProductException(Guid productId)
            : base($"Failed to delete product with ID: {productId}.")
        {
            ProductId = productId;
        }

        public DeleteProductException(Guid productId, Exception innerException)
            : base($"Failed to delete product with ID: {productId}.", innerException)
        {
            ProductId = productId;
        }
    }
}
