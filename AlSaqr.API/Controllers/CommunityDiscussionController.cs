using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using System.Data.Entity.Core.Metadata.Edm;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CommunityDiscussionController : ControllerBase
    {

        private readonly ILogger<CommunityDiscussionController> _logger;
        private readonly IDriver _driver;


        public CommunityDiscussionController(ILogger<CommunityDiscussionController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }

        /// <summary>
        /// Get all commmunity discussion given a community id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}")]
        public async Task<IActionResult> GetCommunityDiscussions(
            string userId,
            string communityId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            await using var session = _driver.AsyncSession();
            var communityDiscussions = new List<Dictionary<string, object>>();
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
                        MATCH (community:Community { id: $communityId })-[:DISCUSSION_POSTED]->(communityDiscussion: CommunityDiscussion)
                        OPTIONAL MATCH (communityDiscussion)-[:INVITED_TO_DISCUSSION]->(iUsers: User)
                        OPTIONAL MATCH (communityDiscussion)-[:JOINED_TO_DISCUSSION]->(jUsers: User)
                        // Determine the user's relationship to each community
                        WITH communityDiscussion, iUsers, jUsers,
                            CASE
                              WHEN EXISTS((communityDiscussion)-[:INVITE_REQUESTED_FOR_DISCUSSION]->(user)) THEN 'INVITE_REQUESTED'
                              WHEN EXISTS((user)-[:CREATED_DISCUSSION]->(communityDiscussion)) THEN 'FOUNDER'
                              WHEN EXISTS((communityDiscussion)-[:INVITED_TO_DISCUSSION]->(user)) THEN 'INVITED'
                              WHEN EXISTS((communityDiscussion)-[:JOINED_TO_DISCUSSION]->(user)) THEN 'JOINED'
                              ELSE 'NONE'
                            END AS relationshipType
                          WITH communityDiscussion,
                              collect(DISTINCT iUsers) as invitedUsers,
                              collect(DISTINCT jUsers) as joinedUsers,
                              relationshipType
                          RETURN communityDiscussion, invitedUsers, joinedUsers, relationshipType";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "communityDiscussion", "invitedUsers", "joinedUsers", "relationshipType" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "communityDiscussion"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                        MATCH (user:User {id: $userId})
                        MATCH (community:Community { id: $communityId })-[:DISCUSSION_POSTED]->(communityDiscussion: CommunityDiscussion)
                        OPTIONAL MATCH (communityDiscussion)-[:INVITED_TO_DISCUSSION]->(iUsers: User)
                        OPTIONAL MATCH (communityDiscussion)-[:JOINED_TO_DISCUSSION]->(jUsers: User)
                        // Determine the user's relationship to each community
                        WITH communityDiscussion, iUsers, jUsers,
                            CASE
                              WHEN EXISTS((communityDiscussion)-[:INVITE_REQUESTED_FOR_DISCUSSION]->(user)) THEN 'INVITE_REQUESTED'
                              WHEN EXISTS((user)-[:CREATED_DISCUSSION]->(communityDiscussion)) THEN 'FOUNDER'
                              WHEN EXISTS((communityDiscussion)-[:INVITED_TO_DISCUSSION]->(user)) THEN 'INVITED'
                              WHEN EXISTS((communityDiscussion)-[:JOINED_TO_DISCUSSION]->(user)) THEN 'JOINED'
                              ELSE 'NONE'
                            END AS relationshipType
                          WITH communityDiscussion,
                              collect(DISTINCT iUsers) as invitedUsers,
                              collect(DISTINCT jUsers) as joinedUsers,
                              relationshipType
                          RETURN communityDiscussion, invitedUsers, joinedUsers, relationshipType";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "communityDiscussion", "invitedUsers", "joinedUsers", "relationshipType" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "communityDiscussion"),
                        new Dictionary<string, object> 
                        {
                            { "userId", userId },
                            { "communityId", communityId },
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

                communityDiscussions = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(communityDiscussions, pagination!));

        }

        /// <summary>
        /// Get community discussion based on community discussion id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}")]
        public async Task<IActionResult> GetCommunityDiscussionInfo(
            string userId,
            string communityId,
            string communityDiscussionId)
        {


            if (string.IsNullOrEmpty(communityId))
            {
                return BadRequest("Community Discussion must have an community id");
            }

            if (string.IsNullOrEmpty(communityDiscussionId))
            {
                return BadRequest("Community Discussion must have an id");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                var selectResult = await Neo4jHelpers.ReadAsync(
                                        session,
                                        @"
                                            MATCH(cmtyDisc: CommunityDiscussion { id: $communityDiscussionId})- [:POSTED_DISCUSSION_ON]->(cmty: Community)
                                            OPTIONAL MATCH(cmtyDisc)- [:INVITED_TO_DISCUSSION]->(iUsers: User)
                                            OPTIONAL MATCH(cmtyDisc)- [:JOINED_DISCUSSION]->(jUsers: User)
                                            WITH cmtyDisc as communityDiscussion,
                                            cmty as community,
                                            collect(DISTINCT iUsers) as invitedUsers,
                                            collect(DISTINCT jUsers) as joinedUsers
                                                RETURN communityDiscussion, community, invitedUsers, joinedUsers
                                        ",
                                        new Dictionary<string, object>()
                                        {
                                           {"communityId", communityId }
                                        },
                                        new[] {
                                            "communityDiscussion", "community", "invitedUsers", "joinedUsers"
                                        }
                                    );


                var communityDiscussionInfo = selectResult?.FirstOrDefault();
                if (communityDiscussionInfo == null)
                {
                    return NotFound(new { message = $"Community Discussion not found based on community discussion id {communityId}", success = false });
                }

                return Ok(communityDiscussionInfo);

            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Community Discussion Admin Info error!");
                return StatusCode(500, new { message = "Fetch Community Discussion Admin Info error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Join a public community discussion
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/join")]
        public async Task<IActionResult> JoinCommunity(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromBody] CommunityDiscussion.CommunityDiscussionInviteConfirmationDto request)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(communityDiscussionId)
                || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Username))
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
                        // Match the community discussion node
                        MERGE (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                        // Create the 'JOINED' relationship with a timestamp
                        MERGE (communityDiscussion)-[r:JOINED_TO_DISCUSSION]->(invitedUser)
                        ON CREATE SET r.timestamp = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match joined user
                        MATCH (invitedUser:User {id: $userId})
                        MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                        MATCH (community:Community { id: communityDiscussion.communityId })
                        // Match the community discussion creator
                        MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                        // Create notification connected to admin
                        CREATE (communityDiscussionCreator)-[:NOTIFIED_BY]->(n:Notification {
                            id: ""notification_"" + randomUUID(),
                            message: invitedUser.username + "" joined your discussion of  "" + communityDiscussion.name + "" in the community of "" + community.name + ""."",
                            read: false,
                            relatedEntityId: communityDiscussion.id,
                            link: ""/communities/"" + community.id + ""/"" + communityDiscussion.id,
                            createdAt: datetime(),
                            updatedAt: null,
                            _rev: null,
                            _type: ""notification"",
                            notificationType: ""user_joined_discussion""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityId", communityId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                return Ok(new { success = true, message = "Joined Discussion Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Join Community Discussion error!");
                return StatusCode(500, new { message = "Join Community Discussion error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// Unjoin a community discussion
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/unjoin")]
        public async Task<IActionResult> UnJoinCommunity(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromBody] CommunityDiscussion.CommunityDiscussionInviteConfirmationDto request)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(communityDiscussionId)
                || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Username))
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
                        MATCH (communityDiscussion: CommunityDiscussion { id: $communityDiscussionId })-[ir:INVITED_TO_DISCUSSION]->(u:User { id: $userId })
                        DELETE ir;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                // Delete Joined Relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (communityDiscussion: CommunityDiscussion { id: $communityDiscussionId })-[jr:JOINED_TO_DISCUSSION]->(u:User { id: $userId })
                        DELETE jr;
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                // Delete notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                           // Match the joined User 
                          MATCH (joinedOrInvitedUser:User {id: $userId})
                          MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                          // Match the author who started community discussion
                          MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                          // Find and delete the specific notification
                          MATCH (communityDiscussionCreator)-[r:NOTIFIED_BY]->(n:Notification {
                            relatedEntityId: communityDiscussion.id,
                            notificationType: ""user_joined_discussion""
                          })
                          DELETE r, n
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                return Ok(new { success = true, message = "Left community discussion Successfully" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Left Community Discussion error!");
                return StatusCode(500, new { message = "Left Community Discussion error!", success = false });
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
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> PostRequestJoin(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromBody] CommunityDiscussion.CommunityDiscussionInviteConfirmationDto request)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(communityDiscussionId)
                || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Username))
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
                         // Match the user node
                          MERGE (invitedUser:User {id: $userId})
                          // Match the community node
                          MERGE (communityDiscussion:CommunityDiscussion {id: $communityDiscussionId})
                          // Create the 'INVITE_REQUESTED_FOR_DISCUSSION' relationship with a timestamp
                          MERGE (communityDiscussion)-[r:INVITE_REQUESTED_FOR_DISCUSSION]->(invitedUser)
                          ON CREATE SET r.timestamp = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                //Create notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                          // Match invited user
                          MATCH (invitedUser:User {id: $userId})
                          MATCH (communityDiscussion:CommunityDiscussion {id: $communityDiscussionId})
                          MATCH (community:Community {id: communityDiscussion.communityId})
                          // Match the community discussion creator
                          MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                          // Create notification connected to admin
                          CREATE (communityDiscussionCreator)-[:NOTIFIED_BY]->(n:Notification {
                              id: ""notification_"" + randomUUID(),
                              message: invitedUser.username + "" has requested to join your discussion of  "" + communityDiscussion.name + "" in the community of "" + community.name,
                              read: false,
                              relatedEntityId: communityDiscussion.id,
                              link: ""/communities/"" + community.id + ""/"" + communityDiscussion.id,
                              createdAt: datetime(),
                              updatedAt: null,
                              _rev: null,
                              _type: ""notification"",
                              notificationType: ""user_request_join_discussion""
                          })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );


                return Ok(new { success = true, message = "Request to join community discussion successfully." });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Request to join community discussion error!");
                return StatusCode(500, new { message = "Request to join community discussion error!", success = false });
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
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/{communityId}/{communityDiscussionId}/request-join")]
        public async Task<IActionResult> RequestJoin(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromBody] CommunityDiscussion.AcceptOrDenyCommunityDiscussionInviteConfirmationDto request)
        {
            // Input validation
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) || string.IsNullOrEmpty(communityDiscussionId))
            {
                return BadRequest("Missing required fields.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                if (request.Accept == true)
                {
                    // Add a invite relationship, since accept request to join.
                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                          // Match the user node
                          MERGE (invitedUser:User {id: $userId})
                          // Match the community node
                          MERGE (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                          // Create the 'INVITED_TO_DISCUSSION' relationship with a timestamp
                          MERGE (communityDiscussion)-[r:INVITED_TO_DISCUSSION]->(invitedUser)
                          ON CREATE SET r.timestamp = timestamp()
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityDiscussionId", communityDiscussionId }
                        }
                    );

                    await Neo4jHelpers.WriteAsync(
                        session,
                        @"
                            // Match invited user
                            MATCH (invitedUser:User {id: $userId})
                            MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                            MATCH (community:Community { id: communityDiscussion.communityId })
                            // Match the community discussion creator
                            MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                            // Create notification connected to admin
                            CREATE (communityDiscussionCreator)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: invitedUser.username + "" joined a community discussion of  "" + communityDiscussion.name +  "" found in the community of "" + community.name + ""."",
                                read: false,
                                relatedEntityId: communityDiscussion.id,
                                link: ""/communities/"" + community.id + ""/"" + communityDiscussion.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""user_joined_discussion""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityDiscussionId", communityDiscussionId }
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
                            MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                            MATCH (community:Community { id: communityDiscussion.communityId })
                            // Match the community discussion creator
                            MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                            // Create notification connected to admin
                            CREATE (communityDiscussionCreator)-[:NOTIFIED_BY]->(n:Notification {
                                id: ""notification_"" + randomUUID(),
                                message: invitedUser.username + "" denied from  your community disucssion of  "" + community.name + "" in the commmunity of  "" + communityDiscussion.name + ""."",
                                read: false,
                                relatedEntityId: communityDiscussion.id,
                                link: ""/communities/"" + community.id + ""/"" + communityDiscussion.id,
                                createdAt: datetime(),
                                updatedAt: null,
                                _rev: null,
                                _type: ""notification"",
                                notificationType: ""user_joined_discussion""
                            })
                        ",
                        new Dictionary<string, object>()
                        {
                            { "userId", userId },
                            { "communityDiscussionId", communityDiscussionId }
                        }
                    );
                }

                // Delete REQUEST_INVITE record in neo4j
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})-[r:INVITE_REQUESTED_FOR_DISCUSSION]->(invitedUser:User {id: $userId})
                        DELETE r
                      ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );

                // Delete request notification
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                         // Match the joined User 
                        MATCH (invitedUser:User {id: $userId})
                        MATCH (communityDiscussion: CommunityDiscussion {id: $communityDiscussionId})
                        // Match the author who created the community discussion
                        MATCH (communityDiscussionCreator:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                        // Find and delete the specific notification
                        MATCH (communityDiscussionCreator)-[r:NOTIFIED_BY]->(n:Notification {
                        relatedEntityId: communityDiscussion.id,
                        notificationType: ""user_request_join_discussion""
                        })
                        DELETE r, n
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "communityDiscussionId", communityDiscussionId }
                    }
                );
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Error accepting or deny request to join community discussion!");
                return StatusCode(500, new { message = "Error accepting or deny request to join community discussion!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// Get admin community discussion based on community discussion id
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}/admin")]
        public async Task<IActionResult> GetAdminCommunityDiscussionInfo(
            string userId,
            string communityId,
            string communityDiscussionId)
        {

            if (string.IsNullOrEmpty(communityId))
            {
                return BadRequest("Community Discussion must have an community id");
            }

            if (string.IsNullOrEmpty(communityDiscussionId))
            {
                return BadRequest("Community Discussion must have an id");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                var selectResult = await Neo4jHelpers.ReadAsync(
                                        session,
                                        @"
                                            MATCH (communityDiscussion:CommunityDiscussion {id: $communityDiscussionId})-[:POSTED_DISCUSSION_ON]->(cmty:Community)
                                            // Get community discussion creator information and check if the specified user is the founder
                                            OPTIONAL MATCH (founder:User)-[:CREATED_DISCUSSION]->(communityDiscussion)
                                            WITH communityDiscussion, founder,
        
                                            EXISTS((:User {id: $userId})-[:CREATED_DISCUSSION]->(communityDiscussion)) AS isFounder
                                            // Get the invite requested users
                                            OPTIONAL MATCH (communityDiscussion)-[:INVITE_REQUESTED_FOR_DISCUSSION]->(inviteRequestedByUser:User)
                                            WITH communityDiscussion, founder, isFounder as isFounder, 
                                              COLLECT(DISTINCT inviteRequestedByUser) AS inviteRequestedUsers

                                            // Count invited users
                                            OPTIONAL MATCH (communityDiscussion)-[:INVITED_TO_DISCUSSION]->(invitedUser:User)
                                            WITH communityDiscussion, founder, isFounder as isFounder, inviteRequestedUsers,
                                                COUNT(DISTINCT invitedUser) AS invitedCount
            
                                            // Count joined users
                                            OPTIONAL MATCH (communityDiscussion)-[:JOINED_TO_DISCUSSION]->(joinedUser:User)
                                            WITH communityDiscussion, founder, isFounder as isFounder, inviteRequestedUsers, invitedCount,
                                                COUNT(DISTINCT joinedUser) AS joinedCount


                                            RETURN communityDiscussion,
                                              isFounder,
                                              founder,
                                              inviteRequestedUsers,
                                              invitedCount,
                                              joinedCount
                                        ",
                                        new Dictionary<string, object>()
                                        {
                                           {"userId", userId },
                                           {"communityDiscussionId", communityDiscussionId },
                                        },
                                        new[] {
                                            "communityDiscussion", "isFounder", "founder", "inviteRequestedUsers", "invitedCount", "joinedCount"
                                        }
                                    );


                var adminCommunityDiscussionInfo = selectResult?.FirstOrDefault();
                if (adminCommunityDiscussionInfo == null)
                {
                    return NotFound(new { message = $"Admin Community Discussion not found based on community discussion id {communityId}", success = false });
                }

                return Ok(adminCommunityDiscussionInfo);

            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch Community Discussion Admin Info error!");
                return StatusCode(500, new { message = "Fetch Community Discussion Admin Info error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Get messages for a given community discussion
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}/{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> GetCommunityDiscussionMessages(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null)
        {
            await using var session = _driver.AsyncSession();
            var communityDiscussions = new List<Dictionary<string, object>>();
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
                        MATCH (cd: CommunityDiscussion { id: $communityDiscussionId })-[:DISCUSSION_MESSAGE_POSTED]->(communityDiscussionMessage: CommunityDiscussionMessage)
                        (user: User {id: communityDiscussionMessage.userId})
                        WHERE communityDiscussionMessage.messageText CONTAINS $searchTerm
                        WITH 
                        user.username as username,
                        user.profileImg as profileImg,
                        communityDiscussionMessage
                        RETURN communityDiscussionMessage, username, profileImg";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "communityDiscussionId", communityDiscussionId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "communityDiscussionMessage" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "communityDiscussionMessage"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "communityDiscussionId", communityDiscussionId },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                        MATCH (cd: CommunityDiscussion { id: $communityDiscussionId })-[:DISCUSSION_MESSAGE_POSTED]->(communityDiscussionMessage: CommunityDiscussionMessage),
                        (user: User {id: communityDiscussionMessage.userId})
                        WITH
                        user.username as username,
                        user.profileImg as profileImg,
                        communityDiscussionMessage
                        RETURN communityDiscussionMessage, username, profileImg";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "communityDiscussionId", communityDiscussionId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "communityDiscussionMessage" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "communityDiscussionMessage"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "communityId", communityId },
                            { "communityDiscussionId", communityDiscussionId },
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

                communityDiscussions = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(communityDiscussions, pagination!));

        }


        /// <summary>
        /// Create a request to join
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="communityId"></param>
        /// <param name="communityDiscussionId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("{userId}/{communityId}/{communityDiscussionId}/messages")]
        public async Task<IActionResult> CreateMessage(
            string userId,
            string communityId,
            string communityDiscussionId,
            [FromBody] CommunityDiscussion.CommunityDiscussionMessageDto request)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(communityId) 
                || string.IsNullOrEmpty(communityDiscussionId) || string.IsNullOrEmpty(request.MessageText))
            {
                return BadRequest("Missing required fields");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                var communityDiscussioMessageId = $"communityDiscussionMsg_${Guid.NewGuid()}";
                // Create a message record
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Create a new community discussion message record 
                        MERGE (u:User {id: $userId})
                        MERGE (cmty:Community {id: $communityId})
                        MERGE (cmtyDisc:CommunityDiscussion {id: $communityDiscussionId})
                        CREATE (cmtyDiscMsg:CommunityDiscussionMessage {
                          id: $id,
                          userId: $userId,
                          communityId: $communityId,
                          communityDiscussionId: $communityDiscussionId,
                          messageText: $messageText,
                          image: $image,
                          updatedAt: null,
                          _rev: """",
                          _type: ""community_discussion"",
                          tags: $tags
                        })
                        CREATE (u)-[:POST_DISCUSSION_MESSAGE {timestamp: datetime()}]->(cmtyDiscMsg)
                        CREATE (cmtyDisc)-[:DISCUSSION_MESSAGE_POSTED {timestamp: datetime()}]->(cmtyDiscMsg)
                        CREATE (cmtyDiscMsg)-[:DISCUSSION_MESSAGED_ON {timestamp: datetime()}]->(cmtyDisc)
                    ",
                    new Dictionary<string, object>()
                    {
                        { "id", communityDiscussioMessageId },
                        { "userId", userId },
                        { "communityId", communityId },
                        { "communityDiscussionId", communityDiscussionId },
                        { "messageText", request.MessageText },
                        { "image", request.Image },
                        { "tags", request.Tags },
                    }
                );


                return Ok(new { success = true, message = "Sent community discussion message successfully!:)" });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Sending of community discussion message error!");
                return StatusCode(500, new { message = "Sending of community discussion message error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

    }

}
