using AlSaqr.Data.Helpers;
using AlSaqr.Domain.Utils;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Newtonsoft.Json;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using System.Text.RegularExpressions;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Products;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class ProductDetailsController : ControllerBase
    {
        private readonly Supabase.Client _supabase;

        public ProductDetailsController(
            Supabase.Client supabase)
        {
            _supabase = supabase;
        }


        /// <summary>
        /// Get product details by id
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpGet("{productId}")]
        public async Task<IActionResult> GetProductDetails(
                int productId,
                [FromQuery] string latitude,
                [FromQuery] string longitude
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
                    await SupabaseHelper.CallFunction(_supabase, functionName, functionParams)
                );

                product = productResult?.FirstOrDefault() ?? new ProductDto();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { product, success = true });
        }


        /// <summary>
        /// Get similar details for a given product
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpGet("{productId}/marquee")]
        public async Task<IActionResult> GetSimilarProductsForMarquee(
                int productId,
                [FromQuery] string latitude,
                [FromQuery] string longitude
            )
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
                    await SupabaseHelper.CallFunction(_supabase, functionName, functionParams)
                );

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { similarProducts, success = true });
        }
    }
}
