using AlSaqr.Data;
using AlSaqr.Data.Entities.Zook;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
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
    public class CategoryController : ControllerBase
    {

        private readonly ILogger<CategoryController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;

        public CategoryController(
            ILogger<CategoryController> logger, 
            Supabase.Client supabase,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
        }

   
        /// <summary>
        /// Get category by id
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        [HttpGet("{categoryId}")]
        public async Task<IActionResult> GetCategory(int categoryId)
        {
            var cachedProductCategory = _userCacheService.GetProductCategory(categoryId);
            ProductCategoryDto? productCategory = null;

            try
            {
                IPostgrestTable<ProductCategory>? selectResult = null;

                selectResult = _supabase.From<ProductCategory>()
                                        .Filter("id", Operator.Equals, categoryId);

                if (cachedProductCategory == null)
                    productCategory = (await selectResult.Get()).Models.Select(pc => new ProductCategoryDto
                    {
                        Id = pc.Id,
                        Name = pc.Name
                    }).FirstOrDefault();
                else
                    productCategory = cachedProductCategory;

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return Ok(new { productCategory, success = true });
        }

    }
}
