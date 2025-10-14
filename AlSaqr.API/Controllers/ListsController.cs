using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using static AlSaqr.API.Utils.Common;
using static AlSaqr.API.Utils.Community;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ListsController : ControllerBase
    {

        private readonly ILogger<ListsController> _logger;
        private readonly IDriver _driver;


        public ListsController(ILogger<ListsController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }


        [HttpGet("{userId}")]
        public async Task<IActionResult> GetLists(
            string userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            await using var session = _driver.AsyncSession();
            var lists = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    selectQuery = @"
                        MATCH (u:User {id:  $userId})-[:CREATED_LIST]->(list:List)
                        WHERE list.text CONTAINS $searchTerm
                        OPTIONAL MATCH (list)-[:SAVED]->(savedByUser:User)
                        WITH list,
                              savedByUser AS savedBy
                        ORDER BY list.createdAt DESCENDING
                        RETURN list,
                              savedBy
                    ";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "list", "savedBy" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "list"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                        MATCH (u:User {id: $userId})-[:CREATED_LIST]->(list:List)
                        OPTIONAL MATCH (list)-[:LIST_CREATOR]->(savedByUser:User)
                        WITH list,
                              savedByUser AS savedBy
                        ORDER BY list.createdAt DESCENDING
                        RETURN list,
                              savedBy
                    ";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "list", "savedBy" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "list"),
                        new Dictionary<string, object> { },
                        new[] { "total" }
                    );
                }

                int totalItems = pagingResult?.FirstOrDefault()?["total"] is long total ? (int)total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };

                lists = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(lists, pagination!));
        }

        /// <summary>
        /// Create a list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateList(
                [FromRoute] string userId,
                [FromBody] List.CreateListFormDto data)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            await using var session = _driver.AsyncSession();

            if (string.IsNullOrEmpty(data?.Name))
            {
                return BadRequest("Name of List is required");
            }

            try
            {
                var listId = $"list_{Guid.NewGuid()}";
                var mutateCipher = @"
                    MERGE (u:User {id: $userId})
                    CREATE (u)-[:CREATED_LIST]->(l:List {
                      id: $id,
                      userId: $userId, 
                      name: $name, 
                      avatar: null,
                      bannerImage: $bannerImage,
                      tags: $tags,
                      createdAt: datetime(),
                      updatedAt: null,
                      _rev: """",
                      _type: ""list""
                    })
                    CREATE (u)-[:CREATED_LIST {timestamp: datetime()}]->(l)
                    CREATE (l)-[:LIST_CREATOR {timestamp: datetime()}]->(u)
                ";

                await Neo4jHelpers.WriteAsync(
                    session,
                    mutateCipher,
                    new Dictionary<string, object>()
                    {
                        { "id", listId },
                        { "userId", userId },
                        { "name", data.Name },
                        { "bannerImage", data.AvatarOrBannerImage },
                        { "tags", data.Tags ?? new string[0] }
                    }
                );


                // Add users added
                if (data.UsersAdded != null && data.UsersAdded.Length > 0)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            UNWIND $usersAdded AS usersAddedId
                            MATCH (list: List {id: $listId}), (user:User {id: usersAddedId})
                            CREATE (list)-[r:SAVED_LIST_ITEM]->(listItem:ListItem {
                                  id: apoc.text.format(""listItem_%s"", [randomUUID()]),
                                  savedUserId: usersAddedId,
                                  postId: null,
                                  commmunityId: null,
                                  communityDiscussionId: null,
                                  communityDiscussionMessageId: null,
                                  listId: $listId,
                                  listItemType: 'user',
                                  savedAt: datetime()
                            })
                            MERGE (listItem)-[lr:SAVED_TO_LIST]->(list)
                            SET r.createdAt = datetime(),
                                lr.createdAt = datetime()
                            RETURN count(lr) AS relationshipsCreated

                        ",
                        new Dictionary<string, object>()
                        {
                            { "listId", listId },
                            { "usersAdded", data.UsersAdded }
                        }
                    );
                }

                // Add posts added
                if (data.PostsAdded != null && data.PostsAdded.Length > 0)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            UNWIND $postsAdded AS postsAddedId
                            MATCH (list: List {id: $listId}), (post:Post {id: postsAddedId})
                            CREATE (list)-[r:SAVED_LIST_ITEM]->(listItem:ListItem {
                                  id: apoc.text.format(""listItem_%s"", [randomUUID()]),
                                  savedUserId: null,
                                  postId: postsAddedId,
                                  commmunityId: null,
                                  communityDiscussionId: null,
                                  communityDiscussionMessageId: null,
                                  listId: $listId,
                                  listItemType: 'post',
                                  savedAt: datetime()
                            })
                            MERGE (listItem)-[lr:SAVED_TO_LIST]->(list)
                            SET r.createdAt = datetime(),
                                lr.createdAt = datetime()
                            RETURN count(lr) AS relationshipsCreated

                        ",
                        new Dictionary<string, object>()
                        {
                            { "listId", listId },
                            { "postsAdded", data.PostsAdded }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating list: {err.Message}");
                return StatusCode(500, new { message = "Add list error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }


        [HttpGet("{userId}/{listId}")]
        public async Task<IActionResult> GetListItems(
            string userId,
            string listId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            await using var session = _driver.AsyncSession();
            var savedListItems = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (string.IsNullOrEmpty(listId))
                    return BadRequest("Must have a list id to get saved list items.");

                selectQuery = @"
                    MATCH (list { id: $listId })-[r:SAVED_LIST_ITEM]->(listItem:ListItem)
                    OPTIONAL MATCH (post: Post { id: listItem.postId })<-[:POSTED]-(postUser: User)
                    OPTIONAL MATCH (cmty: Community { id: listItem.communityId })<-[:COMMUNITY_FOUNDER]-(cmtyFounder: User)
                    OPTIONAL MATCH (cmtyDisc: CommunityDiscussion { id: listItem.communityDiscussionId })<-[:CREATED_DISCUSSION]-(cmtyDiscUser: User)
                    OPTIONAL MATCH (cmtyDiscMsg: CommunityDiscussionMessage { id: listItem.communityDiscussionMessageId })<-[:POST_DISCUSSION_MESSAGE]-(cmtyDiscMsgUser: User)
                    OPTIONAL MATCH (user: User { id: listItem.savedUserId })

                    WITH 
                      listItem, post, postUser, cmty, cmtyFounder, cmtyDisc, cmtyDiscUser, cmtyDiscMsg, cmtyDiscMsgUser, user
          
                    RETURN 
                      listItem,
                      CASE 
                        WHEN cmty IS NOT NULL THEN {
                          community: cmty,
                          founder: cmtyFounder.username,
                          founderProfileImg: cmtyFounder.avatar
                        }
                        WHEN cmtyDisc IS NOT NULL THEN cmtyDisc
                        WHEN cmtyDiscMsg IS NOT NULL THEN {
                          username: cmtyDiscMsgUser.username,
                          profileImg: cmtyDiscMsgUser.avatar,
                          communityDiscussionMessage: cmtyDiscMsg
                        }
                        WHEN user IS NOT NULL THEN {
                          user: user
                        }
                        WHEN post IS NOT NULL THEN {
                          post: post,
                          username: postUser.username,
                          profileImg: postUser.avatar
                        }
                        ELSE NULL
                      END AS relatedEntity,
                      CASE 
                        WHEN cmty IS NOT NULL THEN ""Community""
                        WHEN cmtyDisc IS NOT NULL THEN ""Community Discussion""
                        WHEN cmtyDiscMsg IS NOT NULL THEN ""Community Discussion Message""
                        WHEN user IS NOT NULL THEN ""User""
                        WHEN post IS NOT NULL THEN ""Post""
                        ELSE NULL
                      END AS label
                ";

                selectResult = await Neo4jHelpers.ReadNestedAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>
                    {
                        { "listId", listId },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage }
                    },
                    new[] { "listItem", "relatedEntity", "label" },
                    "relatedEntity",
                    new[] { "post", "community", "user", "communityDiscussion", "communityDiscussionMessage"  }
                );

                pagingResult = await Neo4jHelpers.ReadAsync(
                    session,
                    Neo4jHelpers.CommonCountCipher(selectQuery, "listItem"),
                    new Dictionary<string, object> 
                    {
                        { "listId", listId }
                    },
                    new[] { "total" }
                );

                int totalItems = pagingResult?.FirstOrDefault()?["total"] is long total ? (int)total : 0;

                pagination = new Pagination
                {
                    ItemsPerPage = itemsPerPage,
                    CurrentPage = currentPage,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / itemsPerPage)
                };

                savedListItems = selectResult ?? new List<Dictionary<string, object>>();
            }
            catch(Exception ex)
            {
                _logger.LogError("Error getting saved list items for this list.");
                return BadRequest("Error getting saved list items for this list.");
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(savedListItems, pagination!));
        }

        /// <summary>
        /// Save item to the list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{listId}")]
        public async Task<IActionResult> SavedItemToList(
            string userId,
            string listId,
            [FromBody] List.SaveItemToListDto request)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(listId))
            {
                return BadRequest("Missing required fields");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                var savingPostToList = (request.Type == "post");

                if (savingPostToList)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            MATCH (list:List {id: $listId, userId: $userId})
                            WHERE NOT EXISTS {
                              MATCH (list)-[:SAVED_LIST_ITEM]->(existing:ListItem {postId: $postId})
                            }
                            WITH list
                            CREATE (list)-[r:SAVED_LIST_ITEM]->(listItem:ListItem {
                              id: apoc.text.format(""listItem_%s"", [randomUUID()]),
                              postId: $postId,
                              listId: $listId,
                              listItemType: 'post',
                              savedAt: datetime(),
                              savedUserId: null,
                              communityId: null,
                              communityDiscussionId: null,
                              communityDiscussionMessageId: null
                            })
                            RETURN CASE WHEN count(r) > 0 THEN 1 ELSE 0 END AS itemCreated
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "listId", listId },
                            { "postId", request.RelatedEntityId }
                        }
                    );
                } 
                else
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          MATCH (list: List {id: $listId, userId: $userId})
                          WHERE NOT EXISTS {
                            MATCH (list)-[:SAVED_LIST_ITEM]->(existingItem:ListItem {listId: $listId, savedUserId: $savedUserId})
                          }
                          WITH list
                          CREATE (list)-[r:SAVED_LIST_ITEM]->(listItem:ListItem {
                            id: apoc.text.format(""listItem_%s"", [randomUUID()]),
                            savedUserId: $savedUserId,
                            postId: null,
                            communityId: null,
                            communityDiscussionId: null,
                            communityDiscussionMessageId: null,
                            listId: $listId,
                            listItemType: 'user',
                            savedAt: datetime()
                          })
                          RETURN count(r) AS relationshipsCreated
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "listId", listId },
                            { "savedUserId", request.RelatedEntityId }
                        }
                    );
                }

                return Ok(new { success = true, message = "Saved item to list Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Saved item to list error!");
                return StatusCode(500, new { message = "Saved item to list error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// Delete list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{listId}")]
        public async Task<IActionResult> DeleteList(
            string userId,
            string listId)
        {
            
            // Input validation
            if(string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(listId))
            {
                return BadRequest("Missing required fields such as user id or list id.");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                      MATCH (listItem: ListItem { listItem: $listId })
                      DETACH DELETE listItem;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "listId", listId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                      MATCH (list: List { id: $listId })
                      WHERE list.userId = $userId
                      DETACH DELETE list;
                    ",
                    new Dictionary<string, object>()
                    {
                      { "userId", userId },
                      { "listId", listId }
                    }
                );
  
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete list error!");
                return StatusCode(500, new { message = "Delete list error!", success = false });
            }
        }

        /// <summary>
        /// Delete saved item from list
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="listId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}/{listId}/{listItemId}")]
        public async Task<IActionResult> DeleteSavedFromList(
            string userId,
            string listId,
            string listItemId)
        {

            // Input validation
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(listId) || string.IsNullOrEmpty(listItemId))
            {
                return BadRequest("Missing required fields such as user id or list id.");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (listItem: ListItem { id: $listItemId, listId: $listId })
                        DETACH DELETE listItem
                    ",
                    new Dictionary<string, object>()
                    {
                        { "listId", listId },
                        { "listItemId", listItemId }
                    }
                );


                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete saved item from list error!");
                return StatusCode(500, new { message = "Delete saved item from list error!", success = false });
            }
        }


    }
}
