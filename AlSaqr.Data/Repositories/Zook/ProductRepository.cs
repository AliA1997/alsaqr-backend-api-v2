
using AlSaqr.Data.Entities.Zook;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Domain.Zook;
using Newtonsoft.Json;
using Supabase.Postgrest;
using System.Text.RegularExpressions;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Products;
using static Supabase.Postgrest.Constants;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlSaqr.Data.Repositories.Zook
{
    public class ProductRepository: IProductRepository
    {
        public ProductRepository()
        {

        }

        public async Task<ProductDto> GetProductDetails(
            Supabase.Client client,
            int productId,
            string latitude,
            string longitude
            ) 
        {
            ProductDto? product = null;
            List<ProductDto>? productResult = new List<ProductDto>();
            try
            {
                string functionName = "get_product_details";
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetProductDetailsParams(
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
            int productId,
            string latitude,
            string longitude)
        {
            List<SimilarProductDto>? similarProducts = new List<SimilarProductDto>();
            try
            {
                string functionName = "get_similar_products";
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetProductDetailsParams(
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
            double? maxDistanceKm)
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
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
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
            int categoryId,
            string latitude,
            string longitude,
            int currentPage,
            int itemsPerPage,
            string? searchTerm)
        {
            var products = new List<ProductDto>();
            var functionName = "get_nearby_products_by_category";
            var pagingFunctionName = "get_nearby_products_by_category_total";

            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                int totalItems;

                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetProductByCategoryParams(
                        latitude: latitude,
                        longitude: longitude,
                        productCategoryId: categoryId,
                        skip: skip,
                        currentPage: currentPage,
                        itemsPerPage: itemsPerPage,
                        maxDistanceKm: null,
                        searchTerm: searchTerm
                );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                                await SupabaseHelper.CallFunction(client, functionName, functionParams)
                            );
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(client, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!);
        }
    
        public async Task<Product> CreateProduct(
            Supabase.Client client,
            string userId,
            CreateProductForm form)
        {
            var recentInsertedId = await client.From<Product>().Count(CountType.Estimated);
            string productSlug = Regex.Replace(input: form.Title, pattern: @"[^a-zA-Z0-9]", replacement: "_").ToLower();
            var model = new Product()
            {
                Id = recentInsertedId + 1,
                Title = form.Title,
                Description = form.Description,
                Price = form.Price,
                Slug = productSlug,
                Attributes = form.Attributes ?? new Dictionary<string, object>() { },
                ProductCategoryId = (int)form.ProductCategoryId,
                Images = form.Images != null ? form.Images : new string[] { },
                Latitude = form.Latitude,
                Longitude = form.Longitude,
                Country = form.Country,
                Tags = form.Tags ?? new string[] { },
                Neo4jUserId = userId,
                CreatedAt = DateTime.UtcNow
            };



            var insertedProduct = (await client.From<Product>().Upsert(model, new QueryOptions()
            {
                Returning = QueryOptions.ReturnType.Representation,
            })).Model;

            return insertedProduct;
        } 
    }
}
