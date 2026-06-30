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
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to create a new product");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            var ct = cts.Token;
            try
            {
                await _productRepository.CreateProduct(
                    _supabase,
                    (loggedInUser.Id ?? Guid.Empty),
                    data,
                    ct
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
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpPut("{productId}")]
        public async Task<IActionResult> UpdateProduct(
            Guid productId,
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
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to update your products");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                await _productRepository.UpdateProduct(_supabase, productId, userId, data, ct);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error updating product: {err.Message}");
                return StatusCode(500, new { message = "Update product error!", success = false });
            }
        }

        /// <summary>
        /// Delete a product
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{productId}")]
        public async Task<IActionResult> DeleteProduct(Guid productId)
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to delete a product.");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                await _productRepository.DeleteProduct(_supabase, productId, userId, ct);

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error deleting product: {err.Message}");
                return StatusCode(500, new { message = "Delete product error!", success = false });
            }
        }
    }
}
