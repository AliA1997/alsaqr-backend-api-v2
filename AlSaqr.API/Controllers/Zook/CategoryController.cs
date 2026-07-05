using AlSaqr.Data.Entities.Zook;
using AlSaqr.Domain.Zook;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using Supabase.Postgrest.Interfaces;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly ILogger<CategoryController> _logger;
        private readonly IUserCacheService _userCacheService;
        private readonly ISocialMediaCacheService _socialMediaCacheService;
        private readonly Supabase.Client _supabase;

        public CategoryController(
            ILogger<CategoryController> logger,
            Supabase.Client supabase,
            IUserCacheService userCacheService,
            ISocialMediaCacheService socialMediaCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _userCacheService = userCacheService;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Get All Categories
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            if (_socialMediaCacheService.CheckIfInitialProductCategories())
                return Ok(
                    new
                    {
                        items = _socialMediaCacheService.GetInitialProductCategories(),
                        success = true,
                    }
                );

            try
            {
                var productCategories = await _supabase.From<ProductCategory>().Get();

                var result = productCategories
                    .Models.Select(pc => new ProductCategoryDto { Id = pc.Id, Name = pc.Name })
                    .ToList();

                _socialMediaCacheService.SetInitialProductCategories(result);

                return Ok(new { items = result, success = true });
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Get category by id
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        [HttpGet("{categoryId}")]
        public async Task<IActionResult> GetCategory(Guid categoryId)
        {
            var cachedProductCategory = _userCacheService.GetProductCategory(categoryId);
            ProductCategoryDto? productCategory = null;

            try
            {
                IPostgrestTable<ProductCategory>? selectResult = null;

                selectResult = _supabase
                    .From<ProductCategory>()
                    .Filter("id", Operator.Equals, categoryId.ToString());

                if (cachedProductCategory == null)
                    productCategory = (await selectResult.Get())
                        .Models.Select(pc => new ProductCategoryDto { Id = pc.Id, Name = pc.Name })
                        .FirstOrDefault();
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
