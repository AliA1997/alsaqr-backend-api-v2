using System.Text.RegularExpressions;
using AlSaqr.Data.Entities.Zook;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : AuthorizedControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IProductRepository _productRepository;

        public ProductsController(
            ILogger<ProductsController> logger,
            Supabase.Client supabase,
            IProductRepository productRepository,
            IUserCacheService userCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _productRepository = productRepository;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Get products
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetNearbyProducts(
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null,
            [FromQuery] double? maxDistanceKm = 25.0
        )
        {
            var result = await _productRepository.GetNearbyProduct(
                _supabase,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm,
                maxDistanceKm
            );

            return Ok(result);
        }

        /// <summary>
        /// Get products by category
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{categoryId}")]
        public async Task<IActionResult> GetNearbyProductsByCategory(
            Guid categoryId,
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null
        )
        {
            var result = await _productRepository.NearbyProductsByCategory(
                _supabase,
                categoryId,
                latitude,
                longitude,
                currentPage,
                itemsPerPage,
                searchTerm
            );

            return Ok(result);
        }

        /// <summary>
        /// Create a product
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateProduct(
            [FromBody] AlSaqrUpsertRequest<CreateProductForm> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;

            if (
                string.IsNullOrEmpty(data.Title)
                || string.IsNullOrEmpty(data.Description)
                || data.ProductCategoryId == null
                || data.Latitude == null
                || data.Longitude == null
                || string.IsNullOrEmpty(data.Country)
            )
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                var insertedProduct = await _productRepository.CreateProduct(
                    _supabase,
                    (loggedInUser.Id ?? Guid.Empty),
                    data
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
        }

        /// <summary>
        /// Update a product
        /// </summary>
        /// <param name="categoryId"></param>
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpPut("{categoryId}/{productId}")]
        public async Task<IActionResult> UpdateProduct(
            int productId,
            [FromBody] AlSaqrUpsertRequest<UpsertProductForm> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;

            if (
                string.IsNullOrEmpty(data.Title)
                || string.IsNullOrEmpty(data.Description)
                || data.ProductCategoryId == null
                || data.Latitude == null
                || data.Longitude == null
                || string.IsNullOrEmpty(data.Country)
            )
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                var productToUpdate = (
                    await _supabase.From<Product>().Filter("id", Operator.Equals, productId).Get()
                ).Model;
                bool updateTitle = data.FieldsToUpdate.Contains("title");
                string productSlug = productToUpdate.Slug;

                if (productToUpdate.UserId.ToString() != loggedInUser.Id.ToString())
                    return Unauthorized();

                if (updateTitle)
                    productSlug = Regex
                        .Replace(input: data.Title, pattern: @"[^a-zA-Z0-9]", replacement: "_")
                        .ToLower();

                var model = new Product()
                {
                    Id = productToUpdate.Id,
                    Title = updateTitle ? data.Title : productToUpdate.Title,
                    Description = data.FieldsToUpdate.Contains("description")
                        ? data.Description
                        : productToUpdate.Description,
                    Price = data.FieldsToUpdate.Contains("price")
                        ? data.Price
                        : productToUpdate.Price,
                    Slug = productSlug,
                    Attributes = data.FieldsToUpdate.Contains("attributes")
                        ? data.Attributes ?? new Dictionary<string, object>() { }
                        : productToUpdate.Attributes,
                    ProductCategoryId = data.FieldsToUpdate.Contains("product_category_id")
                        ? (Guid)data.ProductCategoryId
                        : productToUpdate.ProductCategoryId,
                    Images = data.FieldsToUpdate.Contains("images")
                        ? data.Images != null
                            ? data.Images
                            : new string[] { }
                        : productToUpdate.Images,
                    Latitude = data.FieldsToUpdate.Contains("latitude")
                        ? data.Latitude
                        : productToUpdate.Latitude,
                    Longitude = data.FieldsToUpdate.Contains("longitude")
                        ? data.Longitude
                        : productToUpdate.Longitude,
                    Country = data.FieldsToUpdate.Contains("country")
                        ? data.Country
                        : productToUpdate.Country,
                    Tags = data.FieldsToUpdate.Contains("country")
                        ? data.Tags ?? new string[] { }
                        : productToUpdate.Tags,
                };

                var updatedProduct = (
                    await _supabase
                        .From<Product>()
                        .Upsert(
                            model,
                            new QueryOptions()
                            {
                                Returning = QueryOptions.ReturnType.Representation,
                            }
                        )
                ).Model;

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
        }

        /// <summary>
        /// Delete a product
        /// </summary>
        /// <returns></returns>
        [HttpPut("{productId}")]
        public async Task<IActionResult> DeleteProduct(
            int productId,
            CancellationToken cancellationToken
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                var productToDelete = (
                    await _supabase.From<Product>().Filter("id", Operator.Equals, productId).Get()
                ).Model;

                if (
                    productToDelete == null
                    || productToDelete?.UserId.ToString() != loggedInUser.Id.ToString()
                )
                    return Unauthorized();

                await productToDelete.Delete<Product>(cancellationToken);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
        }
    }
}
