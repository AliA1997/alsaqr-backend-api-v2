using System.Text.RegularExpressions;
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.Zook;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Domain.Zook;
using AlSaqr.Domain.Zook.Exceptions;
using Newtonsoft.Json;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.Zook
{
    public class ProductRepository : IProductRepository
    {
        public ProductRepository() { }

        public async Task<ProductDto> GetProductDetails(
            Supabase.Client client,
            Guid productId,
            string latitude,
            string longitude
        )
        {
            ProductDto? product = null;
            List<ProductDto>? productResult = new List<ProductDto>();
            try
            {
                string functionName = "get_product_details";
                IDictionary<string, object> functionParams =
                    SupabaseHelper.DefineGetProductDetailsParams(
                        productId: productId,
                        latitude: latitude,
                        longitude: longitude
                    );

                productResult = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );

                product = productResult?.FirstOrDefault() ?? new ProductDto();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return product;
        }

        public async Task<List<SimilarProductDto>> GetSimilarProducts(
            Supabase.Client client,
            Guid productId,
            string latitude,
            string longitude
        )
        {
            List<SimilarProductDto>? similarProducts = new List<SimilarProductDto>();
            try
            {
                string functionName = "get_similar_products";
                IDictionary<string, object> functionParams =
                    SupabaseHelper.DefineGetProductDetailsParams(
                        productId: productId,
                        latitude: latitude,
                        longitude: longitude
                    );

                similarProducts = JsonConvert.DeserializeObject<List<SimilarProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return similarProducts;
        }

        public async Task<PaginatedResult<ProductDto>> GetNearbyProduct(
            Supabase.Client client,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm,
            double? maxDistanceKm
        )
        {
            var products = new List<ProductDto>();
            var functionName = "get_nearby_products";
            var pagingFunctionName = "get_nearby_products_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetProductParams(
                    latitude: latitude,
                    longitude: longitude,
                    skip: skip,
                    currentPage: currentPage,
                    itemsPerPage: itemsPerPage,
                    maxDistanceKm: null,
                    searchTerm: searchTerm
                );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(
                    await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams),
                    out var total
                );
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!);
        }

        public async Task<PaginatedResult<ProductDto>> NearbyProductsByCategory(
            Supabase.Client client,
            Guid categoryId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm
        )
        {
            var products = new List<ProductDto>();
            var functionName = "get_nearby_products_by_category";
            var pagingFunctionName = "get_nearby_products_by_category_total";

            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                int totalItems;

                IDictionary<string, object> functionParams =
                    SupabaseHelper.DefineGetProductByCategoryParams(
                        latitude: latitude,
                        longitude: longitude,
                        productCategoryId: categoryId,
                        skip: skip,
                        currentPage: currentPage,
                        itemsPerPage: itemsPerPage,
                        maxDistanceKm: 100,
                        searchTerm: searchTerm
                    );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var parsedSuccessfully = int.TryParse(
                    await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams),
                    out var total
                );
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!);
        }

        /// <summary>
        /// Browses the products belonging to one user, addressed by username, paginated
        /// and ordered most-recently-created first (spec PROD-1..PROD-6). Resolving the
        /// profile by username is a precondition: an unknown username raises (mirrors
        /// GetJoinedGroups / GetAttendedEvents), while a known user with no products — or
        /// a search term with no hits — yields an empty, well-formed page (PROD-6).
        /// </summary>
        public async Task<PaginatedResult<ProductDto>> GetUserProducts(
            Supabase.Client client,
            string username,
            int currentPage,
            int itemsPerPage,
            string? searchTerm
        )
        {
            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            var user = await client
                .From<AlSaqrUser>()
                .Where(x => x.Username == username)
                .Single(ct);
            var userId = user.Id;

            var products = new List<ProductDto>();
            var skip = (currentPage - 1) * itemsPerPage;
            IDictionary<string, object> totalParams = new Dictionary<string, object>()
            {
                { "p_user_id", userId.ToString() },
            };

            // Products are sourced directly from the products table (PROD-2), scoped to
            var baseQuery = client
                .From<Product>()
                .Filter("user_id", Operator.Equals, userId.ToString());

            if (!string.IsNullOrEmpty(searchTerm))
            {
                totalParams.Add("p_search_term", searchTerm);
                baseQuery = baseQuery.Filter("title", Operator.ILike, $"%{searchTerm}%");
            }

            var result = await SupabaseHelper.CallFunction(
                client,
                "get_profile_products_count",
                totalParams
            );
            var totalItems = result != null ? long.Parse(result) : 0;

            if (totalItems == 0)
            {
                return new PaginatedResult<ProductDto>(
                    products,
                    new Pagination
                    {
                        ItemsPerPage = itemsPerPage,
                        CurrentPage = currentPage,
                        TotalItems = 0,
                        TotalPages = 0,
                    }
                );
            }

            // Deterministic ordering: created_at desc with a unique tie-breaker on id so
            // paging never duplicates or skips a row (PROD-3/PROD-4, §3.2).
            products = (
                await baseQuery
                    .Order("created_at", Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get(ct)
            )
                .Models.Select(MapToProductDto)
                .ToList();

            var pagination = new Pagination
            {
                ItemsPerPage = itemsPerPage,
                CurrentPage = currentPage,
                TotalItems = (int)totalItems,
                TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
            };

            return new PaginatedResult<ProductDto>(products, pagination);
        }

        public async Task<PaginatedResult<ProductDto>> GetSellingProducts(
            Supabase.Client client,
            Guid userId,
            int currentPage,
            int itemsPerPage,
            string? searchTerm
        )
        {
            var products = new List<ProductDto>();

            var functionName = "get_selling_products";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams =
                    SupabaseHelper.DefineGetSellingProductsParams(
                        skip: skip,
                        itemsPerPage: itemsPerPage,
                        userId: userId.ToString() ?? ""
                    );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                totalItems = 25;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!);
        }

        public async Task<PaginatedResult<ProductDto>> GetBuyingProducts(
            Supabase.Client client,
            Guid userId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm
        )
        {
            var products = new List<ProductDto>();
            var functionName = "get_nearby_products";
            var pagingFunctionName = "get_nearby_products_total";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetProductParams(
                    latitude: latitude,
                    longitude: longitude,
                    skip: skip,
                    currentPage: currentPage,
                    itemsPerPage: itemsPerPage,
                    maxDistanceKm: null,
                    searchTerm: searchTerm
                );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(client, functionName, functionParams)
                );
                var rnd = new Random();
                products = products.OrderBy(_ => rnd.Next()).ToList();
                var parsedSuccessfully = int.TryParse(
                    await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams),
                    out var total
                );
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage),
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!);
        }

        private static ProductDto MapToProductDto(Product p) =>
            new ProductDto
            {
                Id = p.Id,
                UserId = p.UserId ?? Guid.Empty,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                Images = p.Images,
                Slug = p.Slug,
                Attributes = p.Attributes,
                Country = p.Country ?? string.Empty,
                Longitude = (decimal?)p.Longitude,
                Latitude = (decimal?)p.Latitude,
                Tags = p.Tags,
                ProductCategoryId = p.ProductCategoryId,
            };

        public async Task<Product> CreateProduct(
            Supabase.Client client,
            Guid userId,
            CreateProductForm form,
            CancellationToken ct
        )
        {
            try
            {
                string productSlug = Regex
                    .Replace(input: form.Title!, pattern: @"[^a-zA-Z0-9]", replacement: "_")
                    .ToLower();
                var model = new Product()
                {
                    Id = Guid.NewGuid(),
                    Title = form.Title,
                    Description = form.Description,
                    Price = form.Price,
                    Slug = productSlug,
                    Attributes = form.Attributes ?? new Dictionary<string, object>() { },
                    ProductCategoryId =
                        form.ProductCategoryId == null ? Guid.Empty : (Guid)form.ProductCategoryId,
                    Images = form.Images != null ? form.Images : new string[] { },
                    Latitude = form.Latitude,
                    Longitude = form.Longitude,
                    Country = form.Country,
                    Tags = form.Tags ?? new string[] { },
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                };

                var insertedProduct = (
                    await client
                        .From<Product>()
                        .Upsert(
                            model,
                            new QueryOptions() { Returning = ReturnType.Representation },
                            ct
                        )
                ).Model;

                await CreateProductNotification(
                    client,
                    userId,
                    insertedProduct?.Id ?? Guid.Empty,
                    "Listed new product for sale with a title of: {product}",
                    "product_created",
                    CancellationToken.None
                );

                return insertedProduct;
            }
            catch (CreateProductException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new CreateProductException(form.Title, ex);
            }
        }

        public async Task<bool> UpdateProduct(
            Supabase.Client client,
            Guid productId,
            Guid userId,
            UpsertProductForm form,
            CancellationToken ct
        )
        {
            try
            {
                var productToUpdate = await client
                    .From<Product>()
                    .Filter("id", Operator.Equals, productId)
                    .Single(ct);
                if (productToUpdate?.UserId != userId)
                    return false;
                var updateTitle = form.Title?.Trim() != productToUpdate.Title?.Trim();
                var productSlug = productToUpdate.Slug;
                if (updateTitle && !string.IsNullOrEmpty(form.Title))
                    productSlug = Regex
                        .Replace(input: form.Title, pattern: @"[^a-zA-Z0-9]", replacement: "_")
                        .ToLower();

                var model = new Product()
                {
                    Id = productToUpdate.Id,
                    Title = AssignStringValue(productToUpdate.Title, form.Title),
                    Price = form.Price != null ? form.Price : productToUpdate.Price,
                    Slug = productSlug,
                    Attributes =
                        form.Attributes?.Count() > 0 ? form.Attributes : productToUpdate.Attributes,
                    ProductCategoryId =
                        form.ProductCategoryId != Guid.Empty || form.ProductCategoryId != null
                            ? Guid.Parse(form.ProductCategoryId?.ToString())
                            : productToUpdate.ProductCategoryId,
                    Images = form.Images != null ? form.Images : productToUpdate.Images,
                    Latitude = form.Latitude != null ? form.Latitude : productToUpdate.Latitude,
                    Longitude = form.Longitude != null ? form.Longitude : productToUpdate.Longitude,
                    Country = AssignStringValue(productToUpdate.Country, form.Country),
                    Tags = form.Tags != null ? form.Tags : productToUpdate.Tags,
                };

                await client
                    .From<Product>()
                    .Upsert(
                        model,
                        new QueryOptions() { Returning = QueryOptions.ReturnType.Representation },
                        ct
                    );

                await CreateProductNotification(
                    client,
                    userId,
                    productId,
                    "Updated your existing product for sale with a title of: {product}",
                    "product_updated",
                    CancellationToken.None
                );

                return true;
            }
            catch (UpdateProductException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new UpdateProductException(productId, ex);
            }
        }

        public async Task<bool> DeleteProduct(
            Supabase.Client client,
            Guid productId,
            Guid userId,
            CancellationToken ct
        )
        {
            try
            {
                var productToDelete = await client
                    .From<Product>()
                    .Filter("id", Operator.Equals, productId)
                    .Single();

                if (productToDelete == null || productToDelete?.UserId != userId)
                    return false;

                await productToDelete.Delete<Product>(ct);

                await CreateProductNotification(
                    client,
                    userId,
                    productId,
                    "Deleted product for sale with a title of: {product}",
                    "product_deleted",
                    CancellationToken.None
                );

                return true;
            }
            catch (DeleteProductException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeleteProductException(productId, ex);
            }
        }

        private async Task CreateProductNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid productId,
            string messageTemplate,
            string notificationType,
            CancellationToken ct
        )
        {
            var product = await supabase.From<Product>().Where(c => c.Id == productId).Single(ct);

            if (product == null || product.UserId != userId)
                return;

            var actingUser = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single(ct);

            var username = actingUser?.Username ?? "Someone";

            var message = messageTemplate.Replace("{product}", product.Title);

            var notification = new Notification
            {
                UserId = userId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "product",
                ProductId = productId,
                Link = $"/products/{productId}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Minimal }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
