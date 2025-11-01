using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
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
        private readonly IProductRepository _productRepository;
        public ProductDetailsController(
            Supabase.Client supabase,
            IProductRepository productRepository)
        {
            _supabase = supabase;
            _productRepository = productRepository;
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
            var product = await _productRepository.GetProductDetails(
                                                    _supabase,
                                                    productId,
                                                    latitude,
                                                    longitude);

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
            var similarProducts = await _productRepository.GetSimilarProducts(
                                                            _supabase,
                                                            productId,
                                                            latitude,
                                                            longitude);
 
            return Ok(new { similarProducts, success = true });
        }
    }
}
