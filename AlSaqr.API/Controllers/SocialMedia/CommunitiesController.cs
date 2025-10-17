using AlSaqr.Domain.Utils;
using Microsoft.AspNetCore.Mvc;
using Neo4j.Driver;
using AlSaqr.Data;
using static AlSaqr.Domain.Utils.Common;
using static AlSaqr.Domain.Utils.Community;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class CommunitiesController : ControllerBase
    {

        private readonly ILogger<CommunitiesController> _logger;
        private readonly IDriver _driver;

        public CommunitiesController(ILogger<CommunitiesController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        /// <summary>
        /// Returns communities
        /// </summary>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetCommunities(
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {
            await using var session = _driver.AsyncSession();
            var posts = new List<Dictionary<string, object>>();
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
                         MATCH (user:User {id: $userId})
                        // Get all communities in the database
                        MATCH (community:Community)

                        // Get founder for each community (regardless of user relationship)
                        OPTIONAL MATCH (community)-[:COMMUNITY_FOUNDER]->(founder:User)

                        // Determine the user's relationship to each community
                        WITH community, founder, user,
                            CASE
                              WHEN EXISTS((community)-[:INVITE_REQUESTED]->(user)) THEN 'INVITE_REQUESTED'
                              WHEN EXISTS((community)-[:COMMUNITY_FOUNDER]->(user)) THEN 'FOUNDER'
                              WHEN EXISTS((community)-[:INVITED]->(user)) THEN 'INVITED'
                              WHEN EXISTS((user)-[:JOINED]->(community)) THEN 'JOINED'
                              ELSE 'NONE'
                            END AS relationshipType

                        // Return all communities with their relationship status
                        RETURN DISTINCT
                          community,
                          founder,
                          relationshipType
                        ORDER BY relationshipType, community.name";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", "" },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "community", "founder", "relationshipType" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        @"
                            MATCH (user:User {id: $userId})

                            // Get all communities in the database
                            MATCH (community:Community)

                            // Get founder for each community (regardless of user relationship)
                            OPTIONAL MATCH (community)-[:COMMUNITY_FOUNDER]->(founder:User)

                            // Determine the user's relationship to each community
                            WITH community, founder, user,
                                CASE
                                  WHEN EXISTS((community)-[:INVITE_REQUESTED]->(user)) THEN 'INVITE_REQUESTED'
                                  WHEN EXISTS((community)-[:COMMUNITY_FOUNDER]->(user)) THEN 'FOUNDER'
                                  WHEN EXISTS((community)-[:INVITED]->(user)) THEN 'INVITED'
                                  WHEN EXISTS((user)-[:JOINED]->(community)) THEN 'JOINED'
                                  ELSE 'NONE'
                                END AS relationshipType

                            // Return all communities with their relationship status
                            RETURN COUNT(DISTINCT community) as total
                        ",
                        new Dictionary<string, object>
                        {
                            { "userId", "" },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                        MATCH (user:User {id: $userId})

                        // Get all communities in the database
                        MATCH (community:Community)

                        // Get founder for each community (regardless of user relationship)
                        OPTIONAL MATCH (community)-[:COMMUNITY_FOUNDER]->(founder:User)

                        // Determine the user's relationship to each community
                        WITH community, founder, user,
                            CASE
                              WHEN EXISTS((community)-[:INVITE_REQUESTED]->(user)) THEN 'INVITE_REQUESTED'
                              WHEN EXISTS((community)-[:COMMUNITY_FOUNDER]->(user)) THEN 'FOUNDER'
                              WHEN EXISTS((community)-[:INVITED]->(user)) THEN 'INVITED'
                              WHEN EXISTS((user)-[:JOINED]->(community)) THEN 'JOINED'
                              ELSE 'NONE'
                            END AS relationshipType

                        // Return all communities with their relationship status
                        RETURN DISTINCT
                          community,
                          founder,
                          relationshipType
                        ORDER BY relationshipType, community.name";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", "" },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "community", "founder", "relationshipType" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        @"
                            MATCH (user:User {id: $userId})

                            // Get all communities in the database
                            MATCH (community:Community)

                            // Get founder for each community (regardless of user relationship)
                            OPTIONAL MATCH (community)-[:COMMUNITY_FOUNDER]->(founder:User)

                            // Determine the user's relationship to each community
                            WITH community, founder, user,
                                CASE
                                  WHEN EXISTS((community)-[:INVITE_REQUESTED]->(user)) THEN 'INVITE_REQUESTED'
                                  WHEN EXISTS((community)-[:COMMUNITY_FOUNDER]->(user)) THEN 'FOUNDER'
                                  WHEN EXISTS((community)-[:INVITED]->(user)) THEN 'INVITED'
                                  WHEN EXISTS((user)-[:JOINED]->(community)) THEN 'JOINED'
                                  ELSE 'NONE'
                                END AS relationshipType

                            // Return all communities with their relationship status
                            RETURN COUNT(DISTINCT community) as total
                        ",
                        new Dictionary<string, object> 
                        {
                            { "userId", "" }
                        },
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

                posts = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(posts, pagination!));
        }

        /// <summary>
        /// Returns community admin info
        /// </summary>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}")]
        public async Task<IActionResult> GetCommunity(string communityId)
        {
            // Input validation
            if (string.IsNullOrEmpty(communityId))
            {
                return BadRequest("Community ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                string selectQuery = @"
                      MATCH (community:Community {id: $communityId})

                      // Get founder information and check if the specified user is the founder
                      OPTIONAL MATCH (community)-[:COMMUNITY_FOUNDER]->(founder:User)
                      WITH community, founder,
                     EXISTS((community)-[:COMMUNITY_FOUNDER]->(:User {id: $userId})) AS isFounder

                     // Get the invite requested users
                     OPTIONAL MATCH (community)-[:INVITE_REQUESTED]->(inviteRequestedByUser:User)
                      WITH community, founder, isFounder as isFounder, 
                          COLLECT(DISTINCT inviteRequestedByUser) AS inviteRequestedUsers

                      // Count invited users
                      OPTIONAL MATCH (community)-[:INVITED]->(invitedUser:User)
                      WITH community, founder, isFounder as isFounder, inviteRequestedUsers,
                          COUNT(DISTINCT invitedUser) AS invitedCount

                      // Count joined users
                      OPTIONAL MATCH (joinedUser:User)-[:JOINED]->(community)
                      WITH community, founder, isFounder as isFounder, inviteRequestedUsers, invitedCount,
                          COUNT(DISTINCT joinedUser) AS joinedCount

                      RETURN community,
                        isFounder,
                        founder,
                        inviteRequestedUsers,
                        invitedCount,
                        joinedCount";

                var selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        { "userId", "" },
                        {"communityId", communityId }
                    },
                    new[] {
                        "community", "isFounder", "founder", "inviteRequestedUsers", "invitedCount", "joinedCount"
                    }
                );

                var communityAdminInfo = selectResult?.FirstOrDefault();

                if (communityAdminInfo == null)
                {
                    return NotFound(new { message = $"Community not found based on community id {communityId}", success = false });
                }

                return Ok(communityAdminInfo);
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Post error!");
                return StatusCode(500, new { message = "Fetch Post error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Create a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CreateCommunity(
                [FromRoute] string userId,
                [FromBody] AlSaqrUpsertRequest<CreateCommunityFormDto> request)
        {
            var data = request.Values;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            await using var session = _driver.AsyncSession();

            if (string.IsNullOrEmpty(data?.Name))
            {
                return BadRequest("Name of Community is required");
            }

            try
            {
                var communityId = $"community_{Guid.NewGuid()}";
                var selectQuery = @"
                    MERGE (u:User {id: $userId})
                    CREATE (cmty:Community {
                        id: $id,
                        userId: $userId,
                        name: $name,
                        avatar: $avatar,
                        bannerImage: null,
                        createdAt: datetime(),
                        updatedAt: null,
                        _rev: """",
                        _type: """"community"""",
                        isPrivate: $isPrivate,
                        tags: $tags
                    })
                    CREATE (u)-[:CREATED_COMMUNITY {timestamp: datetime()}]->(cmty)
                    CREATE (cmty)-[:COMMUNITY_FOUNDER {timestamp: datetime()}]->(u)
                    ";

                await Neo4jHelpers.WriteAsync(
                    session,
                    selectQuery,
                    new Dictionary<string, object>()
                    {
                        { "id", communityId },
                        {"userId", userId },
                        { "name", data.Name },
                        { "avatar", data.AvatarOrBannerImage },
                        { "isPrivate", data.IsPrivate?.ToLower() == "private" },
                        { "tags", data.Tags ?? new string[0] }
                    }
                );


                // Second write operation: Add invited users
                if (data.UsersAdded != null && data.UsersAdded.Length > 0)
                {
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                        UNWIND $usersAdded AS usersAddedId
                        MATCH (cmty: Community {id: $communityId}), (user:User {id: usersAddedId})
                        MERGE (cmty)-[r:INVITED]->(user)
                        SET r.createdAt = datetime()
                        RETURN count(r) AS relationshipsCreated
                        ",
                        new Dictionary<string, object>()
                        {
                            { "communityId", communityId },
                            { "usersAdded", data.UsersAdded }
                        }
                    );
                }

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error creating community: {err.Message}");
                return StatusCode(500, new { message = "Add community error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Update a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPut("{userId}/{communityId}")]
        public async Task<IActionResult> UpdateCommunity(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<UpdateCommunityForm> request)
        {
            var data = request.Values;

            if (userId == null)
            {
                return BadRequest("User ID is required for updating your user.");
            }
            if (communityId == null && data?.Id == null)
            {
                return BadRequest("Community ID is required for updating your user.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                await Neo4jHelpers.WriteAsync(
                       session,
                       @"
                          MATCH(cmty: Community { id: $communityId })
                          MATCH(u: User { id: $userId })
                          WHERE EXISTS((cmty)- [:COMMUNITY_FOUNDER]->(u))
                          SET cmty.name = $name,
                              cmty.avatar = $avatar,
                              cmty.tags = $tags,
                              cmty.isPrivate = $isPrivate,
                              cmty.updatedAt = timestamp()
                      ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        {"communityId", communityId },
                        { "name", data.Name },
                        { "avatar", data.Avatar },
                        { "tags", data.Tags },
                        { "isPrivate", data.IsPrivate?.ToLower() == "private" }
                    }
                );

                return Ok(new { succcess = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error updating community: {err.Message}");
                return StatusCode(500, new { message = "Update community error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }


        /// <summary>
        /// Join a public community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/join")]
        public async Task<IActionResult> JoinCommunity(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                // Add join relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match the user node
                        MERGE (invitedUser:User {id: $userId})
                        // Match the community node
                        MERGE (community:Community {id: $communityId})
                        // Create the 'JOINED' relationship with a timestamp
                        MERGE (invitedUser)-[r:JOINED]->(community)
                        ON CREATE SET r.timestamp = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match joined user
                        MATCH (invitedUser:User {id: $userId})
                        MATCH (community: Community {id: $communityId})
                        // Match the community admin
                        MATCH (communityAdmin:User)-[:CREATED_COMMUNITY]->(community)
                        // Create notification connected to admin
                        CREATE (communityAdmin)-[:NOTIFIED_BY]->(n:Notification {
                            id: ""notification_"" + randomUUID(),
                            message: invitedUser.username + "" joined your community of  "" + community.name + ""."",
                            read: false,
                            relatedEntityId: community.id,
                            link: ""/communities/"" + community.id,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""user_joined""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );
         
                return Ok(new { success = true, message = "Joined Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Join Community error!");
                return StatusCode(500, new { message = "Join Community error!", success = false });
            } finally
            {
               await session.CloseAsync();
            }

        }

        /// <summary>
        /// Unjoin a community
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunity(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                // Delete the invited or joined user.
                // Delete invite relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH(community: Community { id: $communityId })- [ir:INVITED]->(u: User { id: $userId })
                        DELETE ir;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                // Delete Joined Relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          MATCH(u: User { id: $userId })- [jr:JOINED]->(community: Community { id: $communityId })
                          DELETE jr;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                // Delete notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match the joined User 
                          MATCH(joinedOrInvitedUser: User { id: $userId})
                          MATCH(community: Community { id: $communityId})
                          // Match the author who created the community
                          MATCH(communityAdmin: User) - [:CREATED_COMMUNITY]->(community)
                          // Find and delete the specific notification
                          MATCH(communityAdmin) - [r:NOTIFIED_BY]->(n: Notification {
                                relatedEntityId: community.id,
                            notificationType: ""user_joined""
                          })
                          DELETE r, n
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                return Ok(new { success = true, message = "Left community Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Left Community error!");
                return StatusCode(500, new { message = "Left Community error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// Create a request to join
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/{communityId}/request-join")]
        public async Task<IActionResult> PostRequestJoin(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<CommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(data.Email) || string.IsNullOrEmpty(data.Username))
            {
                return BadRequest("Missing required fields");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                //Create INVITE_REQUESTED relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MERGE (invitedUser:User {id: $userId})
                        MERGE (community:Community {id: $communityId})
                        MERGE (community)-[r:INVITE_REQUESTED]->(invitedUser)
                        ON CREATE SET r.timestamp = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                //Create notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (invitedUser:User {id: $userId})
                        MATCH (community:Community {id: $communityId})
                        MATCH (communityAdmin:User)-[:CREATED_COMMUNITY]->(community)
                        CREATE (communityAdmin)-[:NOTIFIED_BY]->(n:Notification {
                            id: ""notification_"" + randomUUID(),
                            message: invitedUser.username + "" has requested to join your community of "" + community.name + ""."",
                            read: false,
                            relatedEntityId: community.id,
                            link: ""/communities/"" + community.id,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""user_request_join""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );


                return Ok(new { success = true, message = "Request to join community successfully." });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Request to join community error!");
                return StatusCode(500, new { message = "Request to join community error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// Accept or deny a request to join
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/request-join")]
        public async Task<IActionResult> RequestJoin(
            string userId,
            string communityId,
            [FromBody] AlSaqrUpsertRequest<AcceptOrDenyCommunityInviteConfirmationDto> request)
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId))
            {
                return BadRequest("Missing required fields.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                if (data.Accept == true)
                {
                    // Add a invite relationship, since accept request to join.
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user node
                          MERGE (invitedUser:User {id: $userId})
                          // Match the community node
                          MERGE (community:Community {id: $communityId})
                          // Create the 'INVITED' relationship with a timestamp
                          MERGE (community)-[r:INVITED]->(invitedUser)
                          ON CREATE SET r.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityId", communityId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match invited user
                            MATCH (invitedUser:User {id: $userId})
                            MATCH (community: Community {id: $communityId})
                            // Match the community admin
                            MATCH (communityAdmin:User)-[:CREATED_COMMUNITY]->(community)
                            // Create notification connected to admin
                            CREATE (communityAdmin)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: invitedUser.username + "" joined your community of  "" + community.name + ""."",
                                read: false,
                                relatedEntityId: community.id,
                                link: ""/communities/"" + community.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""user_joined""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityId", communityId }
                        }
                    );
                }
                else
                {
                    
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                           // Match invited user
                            MATCH (invitedUser:User {id: $userId})
                            MATCH (community: Community {id: $communityId})
                            // Match the community admin
                            MATCH (communityAdmin:User)-[:CREATED_COMMUNITY]->(community)
                            // Create notification connected to admin
                            CREATE (communityAdmin)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: invitedUser.username + "" denied from  your community of  "" + community.name + ""."",
                                read: false,
                                relatedEntityId: community.id,
                                link: ""/communities/"" + community.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""user_joined""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityId", communityId }
                        }
                    );
                }

                // Delete REQUEST_INVITE record in neo4j
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                      MATCH (community:Community {id: $communityId})-[r:INVITE_REQUESTED]->(invitedUser:User {id: $userId})
                      DELETE r
                      ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );

                // Delete request notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                      // Match the joined User 
                      MATCH (invitedUser:User {id: $userId})
                      MATCH (community:Community {id: $communityId})
                      // Match the author who created the community
                      MATCH (communityAdmin:User)-[:CREATED_COMMUNITY]->(community)
                      // Find and delete the specific notification
                      MATCH (communityAdmin)-[r:NOTIFIED_BY]->(n:Notification {
                        relatedEntityId: community.id,
                        notificationType: ""user_request_join""
                      })
                      DELETE r, n
                        ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId }
                    }
                );
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Error accepting or deny request to join community!");
                return StatusCode(500, new { message = "Error accepting or deny request to join community!", success = false });
            } finally
            {
                await session.CloseAsync();
            }

        }
    }
}
