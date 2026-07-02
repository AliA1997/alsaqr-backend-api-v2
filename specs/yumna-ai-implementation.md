# Overview
- New functionality where user would call an ai to message an ai agent. 
- The backend would return a response from the ai agent.
- For each response the AI Agent gives, it update the number of requests that user did that day, and check based on their subscription if they surpassed their limit. 
- If they did, it will return an error message indicating that they went over their limit.
- The AI Agent is called Yumna.


## Implementation Steps
1) Create new entity modals for subscription, subscription daily use, and update the user modal to have a subscriptoin id referencing the subscription.
2) Add a new repository for retrieving subscription data, and daily use. Call it the subscription repository. Create corresponding interface for respoitory, and inject as Scoped services on the Program.cs file.
3) Add a config class for holding google creds in order to use the google gemini flash llm. 
4)  In regards to the AI Modal itself, it would use gemini's flash 3 llm from a newly created service, it, call it the Yumma service. 
5) Also for proper formatting of prompts create a utility class that will create a standard prompt based on a variety of parameters to pass to genimi llm.
6) Add a new controller for contacting the AI Agent. The ai agent is called Yumna.
7) Use the Subscription repository to check how much is their daily use, use if did not surpass daily use limit(30 prompt responses per day), call yumna service, then update daily use.

## Rules
1) Must have standard access token checks before any functionality. 
2) get user id from cache, and parse it properly.
3) Then check their daily use in the subscription repository
4) If they surpase the daily limit of 30 requests, then return a BadRequest status code indicating they are over their daily use.
5) Else if they did not surpose their daily limit, call the Yumna service, to call the llm. Return the parsed response, and assign it to a result variable. 
6) then update the daily use using the subscription repository.

## Acceptance
1) Pass Test or Example of User using Yumna Successfully 
- A user has a subscription daily use of 21 requests, it would call the YumnaService, then update the subscription daily use to 22 requests.

1) Fail Test or Example of User not using Yumna Successfully
- A user has a subscription daily use of 30 requests, when trying to prompt the ai. It would return a BadRequest.
- A user doesn't pass access token, return an Unauthorized status code.
- A user isn't in cache, meaning they never been check on the app, therefore we return an unauthorized status code.

## Out of Scope
1) Don't create a new pattern in regards to access token checks, and checking if user is in cache.

## Reference Code
1) Access Token check
```csharp
    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;
```
2) Check if user is in cache, and parsing user's id.
```csharp
    var user = _userCacheService.GetLoggedInUser();
    Guid.TryParse(user?.Id?.ToString(), out Guid userId);

    if (userId == Guid.Empty)
    {
        return BadRequest("User ID is required for updating your user.");
    }
```
3) Reference code for other functionality to check if promptmessagedto has valid fields,
```csharp
    if (string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username)
    )
    {
        return BadRequest("Missing required fields");
    }

```
4) Reference Code for Defining Entity Classes:
```csharp
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("posts")]
    public class Post : BaseModel
    {
        public Post() { }

        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("content")]
        public string Content { get; set; }
        [Column("post_type")]
        public string PostType { get; set; }
        [Column("related_post_id")]
        public Guid? RelatedPostId { get; set; }
        [Column("avatar")]
        public string? Avatar { get; set; }
        [Column("banner_image")]
        public string? BannerImage { get; set; }
        [Column("tags")]
        public string[]? Tags { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

    }
}
```
5) Reference code for defining an repository class
```csharp
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Entities.SocialMedia.Views;
using AlSaqr.Data.Helpers;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static AlSaqr.Domain.Utils.Common;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class PostRepository: IPostRepository
    {
        public PostRepository() { }

        public async Task<PaginatedResult<PostDto>> GetBookmarkedPosts(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage,
            CancellationToken ct = default)
        {
            var posts = new List<PostDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;

            try
            {
                // 1. Get IDs of posts this user has bookmarked
                var bookmarks = await supabase
                    .From<PostStatus>()
                    .Where(x => x.UserId == userId)
                    .Filter("action", Operator.Equals, "bookmarked")
                    .Get(ct);

                var postIds = bookmarks.Models
                    .Select(x => x.PostId.ToString())
                    .ToList();


                var bookmarkPosts = await supabase
                    .From<Post>()
                    .Where(x => x.UserId == userId)
                    .Get(ct);

                if (!postIds.Any())
                {
                    return new PaginatedResult<PostDto>(
                        posts,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }

                var parameters = new Dictionary<string, object>
                {
                    { "p_post_ids", postIds },
                };

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    parameters.Add("p_search_term", searchTerm);
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_post_details_count", parameters);
                //var result = await supabase.Rpc("get_post_details_count", parameters);

                var totalItems = result != null ? long.Parse(result) : 0;

                // 3. Fetch the current page
                var dataQuery = supabase
                    .From<VwPostDetails>()
                    .Filter("post_id", Operator.In, postIds);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    dataQuery = dataQuery.Filter("content", Operator.ILike, $"%{searchTerm ?? string.Empty}%");
                }

                var pageResult = await dataQuery
                    .Order("post_created_at", Ordering.Descending)
                    .Range(skip, skip + itemsPerPage - 1)
                    .Get(ct);

                posts = pageResult.Models.Select(vwPost => new PostDto(vwPost)).ToList();

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = (int)totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<PostDto>(posts, pagination!);

        }
    
        public async Task<PaginatedResult<PostDto>> GetPosts(
            Supabase.Client supabase,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            var posts = new List<PostDto>();
            Pagination? pagination = null;
            var skip = (currentPage - 1) * itemsPerPage;
            
            try
            {
                using var cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;
                
                var baseQuery = supabase.From<VwPostDetails>().Where(x => x.PostType == "post");
                var totalParams = new Dictionary<string, dynamic>();

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    totalParams.Add("p_search_term", searchTerm);
                    baseQuery = baseQuery.Filter("content", Operator.ILike, $"%{searchTerm}%");
                }

                var result = await SupabaseHelper.CallFunction(supabase, "get_all_posts_count", totalParams);
                var totalItems = result != null ? long.Parse(result) : 0;


                if (totalItems == 0)
                {
                    return new PaginatedResult<PostDto>(
                        posts,
                        new Pagination
                        {
                            ItemsPerPage = itemsPerPage,
                            CurrentPage = currentPage,
                            TotalItems = 0,
                            TotalPages = 0
                        }
                    );
                }


                posts = (await baseQuery.Order("post_created_at", Ordering.Descending)
                                .Range(skip, skip + itemsPerPage - 1)
                                .Get(ct))
                                .Models
                                .Select(vwPost => new PostDto(vwPost))
                                .ToList();

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = (int)totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return new PaginatedResult<PostDto>(posts, pagination!);
        }

        public async Task<PostDto> GetPost(
            Supabase.Client supabase,
            Guid postId)
        {
            try
            {
                var post = await supabase.From<VwPostDetails>().Where(x => x.PostId == postId).Single();

                if (post == null)
                    throw new Exception("Post not found.");

                return new PostDto(post);

            } catch(Exception ex)
            {
                throw ex;
            }
        }

        // -------------------------------------------------------------------------
        // CREATE
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates a new post row and associates it with the given user.
        /// Migrated from Neo4j CREATE (u)-[:POSTED]->(t:Post {...}).
        /// </summary>
        public async Task<Guid> CreatePost(
            Supabase.Client supabase,
            Guid userId,
            Posts.CreatePostDto data,
            CancellationToken ct)
        {
            try
            {
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Content = data.Text,
                    BannerImage = data.Image ?? string.Empty,
                    Tags = data.Tags ?? Array.Empty<string>(),
                    RelatedPostId = null,
                    PostType = "post",
                    CreatedAt = DateTime.UtcNow
                };

                var inserted = await supabase
                    .From<Post>()
                    .Insert(post, new QueryOptions
                    {
                        Returning = QueryOptions.ReturnType.Representation
                    }, ct);

                if (inserted?.Model == null)
                    throw new Exception("Error creating post");

                return inserted.Model.Id;
            }
            catch(CreatePostException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new CreatePostException(userId, ex);
            }

        }

        public async Task<Guid> DeletePost(
            Supabase.Client supabase,
            Guid userId,
            Guid postId)
        {

            using var cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try
            {
                var post = await supabase.From<Post>().Where(x => x.Id == postId && x.UserId == userId).Single(ct);
                if (post == null)
                    throw new DeletePostException(postId, new Exception("Post not found or user not authorized to delete."));

                var postResult = await supabase.From<Post>().Delete(post, new QueryOptions() {
                    Returning = QueryOptions.ReturnType.Representation
                }, ct);

                if (postResult.Model == null || postResult.Model.Id == Guid.Empty)
                    throw new DeletePostException(postId, new Exception("Error deleting post."));

                await DeletePostStatusForPost(supabase, postId, ct);

                return postResult.Model.Id;
            }
            catch (DeletePostException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeletePostException(postId, ex);
            }
        }

        private async Task DeletePostStatusForPost(
            Supabase.Client supabase,
            Guid postId,
            CancellationToken ct)
        {
            try
            {
                await supabase.From<PostStatus>().Where(x => x.PostId == postId).Delete(new QueryOptions()
                {
                    Returning = QueryOptions.ReturnType.Minimal
                }, ct);

                return;
            }
            catch (DeletePostStatusException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                throw new DeletePostStatusException(postId);
            }
        }
    }
}
```
6) Reference code for definin an repository interface
```csharp
using AlSaqr.Domain.SocialMedia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.Data.Repositories.SocialMedia.Impl
{
    public interface IPostRepository
    {
        Task<PaginatedResult<PostDto>> GetBookmarkedPosts(
            Supabase.Client supabase,
            Guid userId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage,
            CancellationToken ct);

        Task<PaginatedResult<PostDto>> GetPosts(
            Supabase.Client supabase,
            string? searchTerm,
            int currentPage,
            int itemsPerPage);

        Task<PostDto> GetPost(
            Supabase.Client supabase,
            Guid postId);

        Task<Guid> CreatePost(
            Supabase.Client supabase,
            Guid userId,
            Posts.CreatePostDto data,
            CancellationToken ct);

        Task<Guid> DeletePost(
           Supabase.Client supabase,
           Guid userId,
           Guid postId);
    }
}
```
7) Reference code for defining a controller:
```csharp
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia;
using AlSaqr.Infrastructure;
using AlSaqr.Infrastructure.SocialMediaCache;
using Microsoft.AspNetCore.Mvc;
using static AlSaqr.Domain.Utils.Common;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class MessagesController : AuthorizedControllerBase
    {
        private readonly ILogger<MessagesController> _logger;
        private readonly Supabase.Client _supabase;
        private readonly IMessageRepository _messageRepository;
        private readonly IUserCacheService _userCacheService;
        private readonly ISocialMediaCacheService _socialMediaCacheService;

        public MessagesController(
            ILogger<MessagesController> logger,
            Supabase.Client supabase,
            IMessageRepository messageRepository,
            IUserCacheService userCacheService,
            ISocialMediaCacheService socialMediaCacheService
        )
        {
            _logger = logger;
            _supabase = supabase;
            _messageRepository = messageRepository;
            _userCacheService = userCacheService;
            _socialMediaCacheService = socialMediaCacheService;
        }

        /// <summary>
        /// Send a message to a user.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("sendMessage")]
        public async Task<IActionResult> SendMessage(
            [FromBody] AlSaqrUpsertRequest<Messages.MessageFormDto> request
        )
        {
            var authError = ValidateAccessToken();
            if (authError != null)
                return authError;

            var data = request.Values;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

            if (userId != data.SenderId)
                return BadRequest("Logged in user can only send this message.");
            if (data.RecipientId == Guid.Empty)
                return BadRequest("Receiver is required.");
            if (string.IsNullOrEmpty(data.Text))
                return BadRequest("Text of the message is required.");

            await _messageRepository.SendMessage(_supabase, userId, data);
            _logger.LogInformation("Message sent.");
            _socialMediaCacheService.ClearInitialMessageThreads(userId);

            return Ok(new { Success = true });
        }
    }
}
```



