using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
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


        public ExploreController(ILogger<ExploreController> logger, IDriver driver, IConfiguration configuration)
        {
            _logger = logger;
            _driver = driver;
            _configuration = configuration;
        }


        [HttpGet]
        public async Task<IActionResult> GetAllNews(
            [FromQuery] string country = "us",
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10)
        {
            try
            {
                // Build the API URL
                var apiUrl = $"https://newsapi.org/v2/top-headlines?country={country}&sortBy=popularity&apiKey={_configuration["NewsApiKey"]}";

                // Fetch recent news from NewsAPI
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to fetch news from NewsAPI");
                }

                var recentNews = await response.Content.ReadFromJsonAsync<Explore.NewsApiResponse>();
                var articles = recentNews?.Articles ?? new List<Explore.Article>();

                // Calculate pagination
                var startIndex = (currentPage - 1) * itemsPerPage;
                var totalArticles = articles.Count;

                // Filter and map articles
                var result = articles
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
                // Build the API URL
                var apiUrl = $"https://newsapi.org/v2/top-headlines?sources=${sourceId}&sortBy=popularity&apiKey=${_configuration["NewsApiKey"]}";

                // Fetch recent news from NewsAPI
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "Failed to fetch news from NewsAPI");
                }

                var recentNews = await response.Content.ReadFromJsonAsync<Explore.NewsApiResponse>();
                var articles = recentNews?.Articles ?? new List<Explore.Article>();

                // Calculate pagination
                var startIndex = (currentPage - 1) * itemsPerPage;
                var totalArticles = articles.Count;

                // Filter and map articles
                var result = articles
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