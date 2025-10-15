using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using NewsAPI;
using NewsAPI.Constants;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExploreController : ControllerBase
    {

        private readonly ILogger<ExploreController> _logger;
        private readonly IDriver _driver;
        private readonly IConfiguration _configuration;
        private readonly NewsApiClient _newsApiClient;

        public ExploreController(
            ILogger<ExploreController> logger, 
            IDriver driver, 
            IConfiguration configuration,
            NewsApiClient newsApiClient)
        {
            _logger = logger;
            _driver = driver;
            _configuration = configuration;
            _newsApiClient = newsApiClient;
        }


        [HttpGet]
        public async Task<IActionResult> GetAllNews(
            [FromQuery] string country = "us",
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10)
        {
            try
            {
                var articleResponse = await _newsApiClient.GetTopHeadlinesAsync(new NewsAPI.Models.TopHeadlinesRequest()
                {
                    Country = Countries.US,
                    Language = Languages.EN,
                });


                if (articleResponse.Status == Statuses.Error)
                {
                    return StatusCode((int)articleResponse.Status, "Failed to fetch news from NewsAPI");
                }
                
                // Calculate pagination
                var startIndex = (currentPage - 1) * itemsPerPage;
                var totalArticles = articleResponse.Articles.Count;

                // Filter and map articles
                var result = articleResponse.Articles
                    .Where(a => !string.IsNullOrEmpty(a.UrlToImage))
                    .Skip(startIndex)
                    .Take(itemsPerPage)
                    .Select(a => new Explore.ExploreToDisplay
                    {
                        Title = a.Title.Length > 75 ? $"{a.Title.Substring(0, 75)}..." : a.Title,
                        Url = a.Url,
                        UrlToImage = a.UrlToImage
                    })
                    .ToList();

                // Create pagination info
                var pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalArticles,
                    TotalPages = (int)Math.Ceiling((double)totalArticles / itemsPerPage)
                };

                var paginatedResult = new PaginatedResult<Explore.ExploreToDisplay>(result, pagination);

                return Ok(paginatedResult);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error occurred while fetching news");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

        [HttpGet("source/{sourceId}")]
        public async Task<IActionResult> GetNewsBasedOnSource(
            string sourceId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10)
        {
            try
            {
                var articlesResponse = await _newsApiClient.GetTopHeadlinesAsync(new NewsAPI.Models.TopHeadlinesRequest()
                {
                    Sources = new List<string> { sourceId },
                });

                if (articlesResponse.Status == Statuses.Error)
                {
                    return StatusCode((int)articlesResponse.Status, "Failed to fetch news from NewsAPI");
                }

                // Calculate pagination
                var startIndex = (currentPage - 1) * itemsPerPage;
                var totalArticles = articlesResponse.Articles.Count;

                // Filter and map articles
                var result = articlesResponse.Articles
                    .Where(a => !string.IsNullOrEmpty(a.UrlToImage))
                    .Skip(startIndex)
                    .Take(itemsPerPage)
                    .Select(a => new Explore.ExploreToDisplay
                    {
                        Title = a.Title.Length > 75 ? $"{a.Title.Substring(0, 75)}..." : a.Title,
                        Url = a.Url,
                        UrlToImage = a.UrlToImage
                    })
                    .ToList();

                var pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalArticles,
                    TotalPages = (int)Math.Ceiling((double)totalArticles / itemsPerPage)
                };

                var paginatedResult = new PaginatedResult<Explore.ExploreToDisplay>(result, pagination);

                return Ok(paginatedResult);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error occurred while fetching news based on source");
                return StatusCode(500, "An error occurred while processing your request");
            }
        }

    }
}