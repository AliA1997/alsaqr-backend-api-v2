using AlSaqr.Data.Entities.Zook;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.Zook.Impl;
using AlSaqr.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using Supabase.Postgrest;
using System.Text.RegularExpressions;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Products;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.API.Controllers.Zook
{
    [ApiController]
    [Route("[controller]")]
    public class ProductsController : ControllerBase
    {

        private readonly ILogger<ProductsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;
        private readonly IProductRepository _productRepository;

        public ProductsController(
            ILogger<ProductsController> logger, 
            IDriver driver,
            Supabase.Client supabase,
            IProductRepository productRepository,
            IUserCacheService userCacheService)
        {
            _logger = logger;
            _driver = driver;
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
                                                    maxDistanceKm);

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
                int categoryId,
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
                                                searchTerm);

            return Ok(result);
        }

        /// <summary>
        /// Create a product
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreateProduct(
            [FromBody] AlSaqrUpsertRequest<CreateProductForm> request)
        {
            await using var session = _driver.AsyncSession();
            var data = request.Values;

            if (string.IsNullOrEmpty(data.Title) || string.IsNullOrEmpty(data.Description) || data.ProductCategoryId == null 
                || data.Latitude == null || data.Longitude == null || string.IsNullOrEmpty(data.Country))
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();

                if(loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a product.");
                }

                var insertedProduct = await _productRepository.CreateProduct(
                                                                _supabase,
                                                                loggedInUser.Id!,
                                                                data);


                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match created product user
                        MATCH (productCreator: User { id: $userId})
                        // Create notification connected to author
                        CREATE (productCreator)-[:NOTIFIED_BY]->(n: Notification {
                            id: ""notification_"" + randomUUID(),
                            message: ""Listed new product for sale with a title of: "" + $productTitle,
                            read: false,
                            relatedEntityId: $productId,
                            link: ""/products/"" + $productCategoryId + ""/"" + $productId,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""created_product""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                      { "userId", loggedInUser.Id  ?? "" },
                      { "productTitle", insertedProduct?.Title ?? "" },
                      { "productCategoryId", insertedProduct?.ProductCategoryId ?? -1 },
                      { "productId", insertedProduct?.Id ?? -1 },
                    }
                 );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
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
            [FromBody] AlSaqrUpsertRequest<UpsertProductForm> request)
        {
            await using var session = _driver.AsyncSession();
            var data = request.Values;

            if (string.IsNullOrEmpty(data.Title) || string.IsNullOrEmpty(data.Description) || data.ProductCategoryId == null
                || data.Latitude == null || data.Longitude == null || string.IsNullOrEmpty(data.Country))
            {
                return BadRequest("Fields are required!");
            }

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();
                if (loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a product.");
                }

                var productToUpdate = (await _supabase.From<Product>().Filter("id", Operator.Equals, productId).Get()).Model;
                bool updateTitle = data.FieldsToUpdate.Contains("title");
                string productSlug = productToUpdate.Slug;

                if (productToUpdate.Neo4jUserId != loggedInUser.Id)
                    return Unauthorized();

                if(updateTitle)
                    productSlug = Regex.Replace(input: data.Title, pattern: @"[^a-zA-Z0-9]", replacement: "_").ToLower();


                var model = new Product()
                {
                    Id = productToUpdate.Id,
                    Title = updateTitle ? data.Title : productToUpdate.Title,
                    Description = data.FieldsToUpdate.Contains("description") ? data.Description : productToUpdate.Description,
                    Price = data.FieldsToUpdate.Contains("price") ? data.Price : productToUpdate.Price,
                    Slug = productSlug,
                    Attributes = data.FieldsToUpdate.Contains("attributes") ? data.Attributes ?? new Dictionary<string, object>() { } : productToUpdate.Attributes,
                    ProductCategoryId = data.FieldsToUpdate.Contains("product_category_id") ? (int)data.ProductCategoryId : productToUpdate.ProductCategoryId,
                    Images = data.FieldsToUpdate.Contains("images") ? data.Images != null ? data.Images : new string[] { } : productToUpdate.Images,
                    Latitude = data.FieldsToUpdate.Contains("latitude") ? data.Latitude : productToUpdate.Latitude,
                    Longitude = data.FieldsToUpdate.Contains("longitude") ? data.Longitude : productToUpdate.Longitude,
                    Country = data.FieldsToUpdate.Contains("country") ? data.Country : productToUpdate.Country,
                    Tags = data.FieldsToUpdate.Contains("country") ? data.Tags ?? new string[] { } : productToUpdate.Tags
                };

                var updatedProduct = (await _supabase.From<Product>().Upsert(model, new QueryOptions()
                {
                    Returning = QueryOptions.ReturnType.Representation,
                })).Model;


                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match created product user
                        MATCH (productCreator: User { id: $userId})
                        // Create notification connected to author
                        CREATE (productCreator)-[:NOTIFIED_BY]->(n: Notification {
                            id: ""notification_"" + randomUUID(),
                            message: ""Updated product for sale with a title of: "" + $productTitle,
                            read: false,
                            relatedEntityId: $productId,
                            link: ""/products/"" + $productCategoryId + ""/"" + $productId,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""updated_product""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                      { "userId", loggedInUser.Id  ?? "" },
                      { "productTitle", updatedProduct?.Title ?? "" },
                      { "productCategoryId", updatedProduct?.ProductCategoryId ?? -1 },
                      { "productId", updatedProduct?.Id ?? -1 },
                    }
                 );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Delete a product
        /// </summary>
        /// <returns></returns>
        [HttpPut("{productId}")]
        public async Task<IActionResult> DeleteProduct(
            int productId, 
            CancellationToken cancellationToken)
        {
            await using var session = _driver.AsyncSession();

            try
            {
                var loggedInUser = _userCacheService.GetLoggedInUser();
                if (loggedInUser == null)
                {
                    return Unauthorized("User must be logged in, in order to create a product.");
                }

                var productToDelete = (await _supabase.From<Product>().Filter("id", Operator.Equals, productId).Get()).Model;

                if (productToDelete.Neo4jUserId != loggedInUser.Id)
                    return Unauthorized();

                await productToDelete.Delete<Product>(cancellationToken);

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match created product user
                        MATCH (productCreator: User { id: $userId})
                        // Create notification connected to author
                        CREATE (productCreator)-[:NOTIFIED_BY]->(n: Notification {
                            id: ""notification_"" + randomUUID(),
                            message: ""Delete a product with a title of: "" + $productTitle,
                            read: false,
                            relatedEntityId: $productId,
                            link: ""n/a"",
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""delete_product""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                      { "userId", loggedInUser.Id  ?? "" },
                      { "productTitle", productToDelete?.Title ?? "" },
                      { "productId", productToDelete?.Id ?? -1 },
                    }
                 );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating product: {err.Message}");
                return StatusCode(500, new { message = "Add product error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

    }
}
