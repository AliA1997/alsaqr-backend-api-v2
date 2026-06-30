using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Supabase.Postgrest.Interfaces;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class UserProductsController : AuthorizedControllerBase
    {
        private readonly ILogger<UserProductsController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;

        public UserProductsController(
            ILogger<UserProductsController> logger,
            IProductRepository productRepository,
            Supabase.Client supabase,
            IUserCacheService userCacheService
        )
        {
            _logger = logger;
            _productRepository = productRepository;
            _supabase = supabase;
            _userCacheService = userCacheService;
        }

        /// <summary>
        /// Get selling products
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("selling")]
        public async Task<IActionResult> GetSellingProducts(
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to update your products");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            try
            {
                var result = await _productRepository.GetSellingProducts(
                    _supabase,
                    userId,
                    currentPage,
                    itemsPerPage,
                    searchTerm
                );

                return Ok(result);
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error getting selling products: {err.Message}");
                return StatusCode(
                    500,
                    new { message = "Retrieving Selling products error!", success = false }
                );
            }
        }

        /// <summary>
        /// Get buying products
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="longitude"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("buying")]
        public async Task<IActionResult> GetBuyingProducts(
            [FromQuery] string latitude,
            [FromQuery] string longitude,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 25,
            [FromQuery] string? searchTerm = null
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null)
                return Unauthorized("Must be logged in to update your products");
            Guid.TryParse(loggedInUser.Id?.ToString(), out var userId);

            try
            {
                var result = await _productRepository.GetBuyingProducts(
                    _supabase,
                    userId,
                    latitude,
                    longitude,
                    currentPage,
                    itemsPerPage,
                    searchTerm
                );

                return Ok(result);
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error getting buying products: {err.Message}");
                return StatusCode(
                    500,
                    new { message = "Retrieving Buying products error!", success = false }
                );
            }
        }
    }
}
