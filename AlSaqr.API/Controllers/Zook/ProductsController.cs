using AlSaqr.Data.Entities.Zook;
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
    public class ProductsController : ControllerBase
    {

        private readonly ILogger<ProductsController> _logger;
        private readonly IDriver _driver;
        private readonly IUserCacheService _userCacheService;
        private readonly Supabase.Client _supabase;

        public ProductsController(
            ILogger<ProductsController> logger, 
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
                var parsedSuccessfully = int.TryParse(await SupabaseHelper.CallFunction(_supabase, pagingFunctionName, functionParams), out var total);
                totalItems = parsedSuccessfully ? total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            } catch(Exception ex)
            {
                throw ex;
            }

            return Ok(new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!));
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
                                await SupabaseHelper.CallFunction(_supabase, functionName, functionParams)
                            );
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
            catch(Exception ex)
            {
                throw ex;
            }

            return Ok(new PaginatedResult<ProductDto>(products ?? new List<ProductDto>(), pagination!));
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
                var recentInsertedId = await _supabase.From<Product>().Count(CountType.Estimated);
                string productSlug = Regex.Replace(input: data.Title, pattern: @"[^a-zA-Z0-9]", replacement: "_").ToLower();
                var model = new Product()
                {
                    Id = recentInsertedId + 1,
                    Title = data.Title,
                    Description = data.Description,
                    Price = data.Price,
                    Slug = productSlug,
                    Attributes = data.Attributes ?? new Dictionary<string, object>() { },
                    ProductCategoryId = (int)data.ProductCategoryId,
                    Images = data.Images != null ? data.Images : new string[] { },
                    Latitude = data.Latitude,
                    Longitude = data.Longitude,
                    Country = data.Country,
                    Tags = data.Tags ?? new string[]{ },
                    Neo4jUserId = loggedInUser.Id,
                    CreatedAt = DateTime.UtcNow
                };

                

                var insertedProduct = (await _supabase.From<Product>().Upsert(model, new QueryOptions() 
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
