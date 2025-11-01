using AlSaqr.Data.Helpers;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Newtonsoft.Json;
using Supabase.Postgrest.Interfaces;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class UserProductsController : ControllerBase
    {

        private readonly ILogger<UserProductsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;

        public UserProductsController(
            ILogger<UserProductsController> logger,
            IDriver driver,
            Supabase.Client supabase,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
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
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (string.IsNullOrEmpty(loggedInUser?.Id))
                return Unauthorized("Need to be logged in to see your product being sold.");

            var products = new List<ProductDto>();
            var functionName = "get_selling_products";
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            try
            {
                int totalItems;
                IDictionary<string, object> functionParams = SupabaseHelper.DefineGetSellingProductsParams(
                            skip: skip,
                            itemsPerPage: itemsPerPage,
                            userId: loggedInUser?.Id ?? ""
                );

                products = JsonConvert.DeserializeObject<List<ProductDto>>(
                    await SupabaseHelper.CallFunction(_supabase, functionName, functionParams)
                );
                totalItems = 25;

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

            return Ok(new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!));

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
                    await SupabaseHelper.CallFunction(_supabase, functionName, functionParams)
                );
                var rnd = new Random();
                products = products.OrderBy(_ => rnd.Next()).ToList();
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(_supabase, pagingFunctionName, functionParams), out var total);
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

            return Ok(new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!));

        }

    }
}
