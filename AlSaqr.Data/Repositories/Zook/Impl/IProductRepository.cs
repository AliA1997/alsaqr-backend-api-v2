using AlSaqr.Data.Entities.Zook;
using AlSaqr.Domain.Zook;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Products;

namespace AlSaqr.Data.Repositories.Zook.Impl
{
    public interface IProductRepository
    {
        Task<ProductDto> GetProductDetails(
            Supabase.Client client,
            int productId,
            string latitude,
            string longitude);

        Task<List<SimilarProductDto>> GetSimilarProducts(
            Supabase.Client client,
            int productId,
            string latitude,
            string longitude);

        Task<PaginatedResult<ProductDto>> GetNearbyProduct(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm);

        Task<PaginatedResult<ProductDto>> NearbyProductsByCategory(
            Supabase.Client client,
            int categoryId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm);

        Task<Product> CreateProduct(
            Supabase.Client client,
            string userId,
            CreateProductForm form);
    }
}
