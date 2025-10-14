using AlSaqr.API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using Newtonsoft.Json;
using static AlSaqr.API.Utils.Common;

namespace AlSaqr.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {

        private readonly ILogger<UsersController> _logger;
        private readonly IDriver _driver;


        public UsersController(ILogger<UsersController> logger, IDriver driver)
        {
            _logger = logger;
            _driver = driver;
        }


        /// <summary>
        /// Update user based on user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>

        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateUser(
            string userId,
            [FromBody] User.UpdateUserDto data)
        {
            if (userId == null)
            {
                return BadRequest("User ID is required for updating your user.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (u:User { id: $userId })
                            SET u.username = $username,
                            u.avatar = $avatar,
                            u.bgThumbnail = $bgThumbnail,
                            u.bio = $bio,
                            u.firstName = $firstName,
                            u.lastName = $lastName,
                            u.dateOfBirth = $dateOfBirth,
                            u.maritalStatus = $maritalStatus,
                            u.hobbies = $hobbies,
                            u.religion = $religion,
                            u.countryOfOrigin = $countryOfOrigin,
                            u.preferredMadhab = $preferredMadhab,
                            u.frequentMasjid = $frequentMasjid,
                            u.favoriteQuranReciters = $favoriteQuranReciters,
                            u.favoriteIslamicScholars = $favoriteIslamicScholars,
                            u.islamicStudyTopics = $islamicStudyTopics,
                            u.updatedAt = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "username", data.Username },
                        { "avatar", data.Avatar },
                        { "bgThumbnail", data.BgThumbnail },
                        { "bio", data.Bio },
                        { "firstName", data.FirstName },
                        { "lastName", data.LastName },
                        { "dateOfBirth", data.DateOfBirth },
                        { "maritalStatus", data.MaritalStatus },
                        { "hobbies", data.Hobbies },
                        { "religion", data.Religion },
                        { "countryOfOrigin", data.CountryOfOrigin },
                        { "preferredMadhab", data.PreferredMadhab },
                        { "frequentMasjid", data.FrequentMasjid },
                        { "favoriteQuranReciters", data.FavoriteQuranReciters },
                        { "favoriteIslamicScholars", data.FavoriteIslamicScholars },
                        { "islamicStudyTopics", data.IslamicStudyTopics }
                    }
                );

                return Ok(new { succcess = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error updating user: {err.Message}");
                return StatusCode(500, new { message = "Update user error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Delete user based on user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            // Input validation
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required");
            }

            await using var session = _driver.AsyncSession();

            try
            {

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (user: User { id: $userId })
                        DETACH DELETE user
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (pst: Post { userId: $userId })
                        DETACH DELETE pst
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId }
                    }
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete user error!");
                return StatusCode(500, new { message = "Delete user error!", success = false });
            }
        }


        /// <summary>
        /// Follow user endpoint
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/follow")]
        public async Task<IActionResult> Follow(
            string userId,
            [FromBody] User.FollowUserFormDto request)
        {
            // Input validation
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required for following someone.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                // Add reposted relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match the user node
                        MERGE (user:User {id: $userId})
                        // Match the user to follow node
                        MERGE (userToFollow:User {id: $userToFollowId})
                        // Create the 'FOLLOW_USER' relationship with a timestamp
                        MERGE (user)-[followUserRel:FOLLOW_USER]->(userToFollow)
                        // Create the 'FOLLOWED' relationship with a timestamp
                        MERGE (userToFollow)-[followedRel:FOLLOWED]->(user)

                        ON CREATE SET followUserRel.timestamp = timestamp()
                        ON CREATE SET followedRel.timestamp = timestamp()
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "userToFollowId", request.UserToFollowId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match following user
                        MATCH (followingUser:User {id: $userId})
                        // Match the followed user (fixed relationship direction)
                        MATCH (followedUser:User {id: $userToFollowId})
                        // Create notification connected to author
                        CREATE (followedUser)-[:NOTIFIED_BY]->(n:Notification {
                          id: ""notification_"" + randomUUID(),
                          message: followingUser.username "" is following you"",
                          read: false,
                          relatedEntityId: followingUser.id,
                          link: ""/users/"" + followingUser.id,
                          createdAt: datetime(),
                          updatedAt: null,
                          _rev: null,
                          _type: ""notification"",
                          notificationType: ""follow_user""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "userToFollowId", request.UserToFollowId }
                    }
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Follow User error!");
                return StatusCode(500, new { message = "Follow User error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }

        }

        /// <summary>
        /// UnFollow user endpoint
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPatch("{userId}/unfollow")]
        public async Task<IActionResult> UnFollow(
            string userId,
            [FromBody] User.UnFollowUserFormDto request)
        {
            // Input validation
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required for unfollowing someone.");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                // Add reposted relationship
                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (user:User {id: $userId})-[followUserRel:FOLLOW_USER]->(userToUnFollow:User {id: $userToUnFollowId})
                        DELETE followUserRel
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "userToUnFollowId", request.UserToUnFollowId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (userToUnFollow:User {id: $userToUnFollowId})-[followedRel:FOLLOWED]->(user:User {id: $userId})
                        DELETE followedRel
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "userToUnFollowId", request.UserToUnFollowId }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        // Match following user
                        MATCH (followingUser:User {id: $userId})
                        // Match the followed user (fixed relationship direction)
                        MATCH (followedUser:User {id: $userToFollowId})
                        MATCH (followedUser)-[r:NOTIFIED_BY]->(n:Notification {
                          relatedEntityId: followingUser.id,
                          notificationType: ""follow_user""
                        })
                        DELETE r, n
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "userToUnFollowId", request.UserToUnFollowId }
                      }
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "UnFollow User error!");
                return StatusCode(500, new { message = "UnFollow User error!", success = false });
            } 
            finally
            {
                await session.CloseAsync();
            }

        }


        /// <summary>
        /// Get posts to add whenever creating entity items such as communities, and lists.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}/postsToAdd")]
        public async Task<IActionResult> GetPostsToAdd(
            string userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10,
            [FromQuery] string? searchTerm = null
        )
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User Id is required for getting posts to add.");

            await using var session = _driver.AsyncSession();
            var postsToAdd = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                var userPostTags = await Neo4jHelpers.ReadAsync(
                    session,
                    @"
                        MATCH(post: Post { userId: $userId })
                        WITH post
                        ORDER BY post.createdAt DESC
                        UNWIND post.tags AS tag
                        WITH DISTINCT tag
                        RETURN collect(tag) AS distinctTags
                    ",
                    new Dictionary<string, object>
                    {
                        { "userId", userId }
                    },
                    new[] { "distinctTags" }
                );

                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    selectQuery = @"
                    MATCH (post:Post), (user: User { id: post.userId })
                        WHERE post.text CONTAINS $searchTerm 
                                AND (
                                    ANY(tag in post.tags WHERE tag IN $userPostTags) AND user.id <> $userId 
                                )  AND  user.id <> $userId 
                        OPTIONAL MATCH (post)-[:HAS_COMMENT]->(c:Comment)<-[:COMMENTED]-(u:User)
                        OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                        OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                        WITH post,
                            user.username as username,
                            user.avatar as profileImg,
                            COLLECT(DISTINCT c) AS comments,
                            COLLECT(DISTINCT u) AS commenters,
                            COLLECT(DISTINCT reposter) AS reposters,
                            COLLECT(DISTINCT liker) AS likers
                        ORDER BY post.createdAt DESCENDING
                        RETURN post,
                              username,
                              profileImg,
                              comments,
                              commenters,
                              reposters,
                              likers";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "userPostTags", userPostTags.ToArray() },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "post", "username", "profileImg", "comments", "commenters", "reposters", "likers" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "userPostTags", userPostTags.ToArray() },
                            { "searchTerm", searchTerm }
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                    MATCH (post:Post), (user: User { id: post.userId })
                        WHERE ANY(tag in post.tags WHERE tag IN $userPostTags) AND user.id <> $userId 
                        OPTIONAL MATCH (post)-[:HAS_COMMENT]->(c:Comment)<-[:COMMENTED]-(u:User)
                        OPTIONAL MATCH (post)-[:RETWEETS]->(reposter:User)
                        OPTIONAL MATCH (post)-[:LIKED]->(liker:User)
                        WITH post,
                            user.username as username,
                            user.avatar as profileImg,
                            COLLECT(DISTINCT c) AS comments,
                            COLLECT(DISTINCT u) AS commenters,
                            COLLECT(DISTINCT reposter) AS reposters,
                            COLLECT(DISTINCT liker) AS likers
                        ORDER BY post.createdAt DESCENDING
                        RETURN post,
                              username,
                              profileImg,
                              comments,
                              commenters,
                              reposters,
                              likers";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "userPostTags", userPostTags.ToArray() },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage }
                        },
                        new[] { "post", "username", "profileImg", "comments", "commenters", "reposters", "likers" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "post"),
                        new Dictionary<string, object> 
                        {
                            { "userId", userId },
                            { "userPostTags", userPostTags.ToArray() },
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

                postsToAdd = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(postsToAdd, pagination!));
        }

        /// <summary>
        /// Get users to add whenever creating entity items such as communities, and lists.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet("{userId}/usersToAdd")]
        public async Task<IActionResult> GetUsersToAdd(
                string userId,
                [FromQuery] int currentPage = 1,
                [FromQuery] int itemsPerPage = 10,
                [FromQuery] string? searchTerm = null
            )
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User Id is required for getting users to add.");

            await using var session = _driver.AsyncSession();
            var usersToAdd = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {
                var users = await Neo4jHelpers.ReadAsync(
                    session,
                    @"
                        MATCH(user: User { id: $userId}) RETURN user
                    ",
                    new Dictionary<string, object>
                    {
                        { "userId", userId }
                    },
                    new[] { "user" }
                );
                User.GetUserResponse? userResponse = users?.FirstOrDefault() != null 
                                                        ? JsonConvert.DeserializeObject<User.GetUserResponse>(JsonConvert.SerializeObject(users?.FirstOrDefault()))
                                                        : null;

                if (userResponse == null || userResponse.User == null)
                    return BadRequest("Logged in user not found.");

                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    selectQuery = @"
                        MATCH (user:User)
                        WHERE (user.username CONTAINS $searchTerm OR user.email CONTAINS $searchTerm) 
                                AND (
                                    ANY(scholar in user.favoriteIslamicScholars WHERE scholar IN $favoriteIslamicScholars) 
                                    OR 
                                    ANY(hobby in user.hobbies WHERE hobby IN $hobbies)
                                    OR
                                    ANY(quranReciter in user.favoriteQuranReciters WHERE quranReciter IN $favoriteQuranReciters)
                                    OR
                                    ANY(islamicStudyTopic in user.islamicStudyTopics WHERE islamicStudyTopic IN $islamicStudyTopics)
                                    OR
                                    user.preferredMadhab = $preferredMadhab
                                )  AND  user.id <> $userId 
                            OPTIONAL MATCH (follower:User)-[:FOLLOWED]->(user)
                            OPTIONAL MATCH (user)-[:FOLLOW_USER]->(following:User)
                            RETURN user,
                                    COLLECT(DISTINCT follower) AS followers,
                                    COLLECT(DISTINCT following) AS following
                    ";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "searchTerm", searchTerm },
                            { "favoriteIslamicScholars",  userResponse.User.FavoriteIslamicScholars },
                            { "hobbies", userResponse.User.Hobbies },
                            { "islamicStudyTopics", userResponse.User.IslamicStudyTopics },
                            { "favoriteQuranReciters", userResponse.User.FavoriteQuranReciters },
                            { "preferredMadhab", userResponse.User.PreferredMadhab },
                        },
                        new[] { "user", "followers", "following" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "user"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "searchTerm", searchTerm },
                            { "favoriteIslamicScholars", userResponse.User.FavoriteIslamicScholars },
                            { "hobbies", userResponse.User.Hobbies },
                            { "islamicStudyTopics", userResponse.User.IslamicStudyTopics },
                            { "favoriteQuranReciters", userResponse.User.FavoriteQuranReciters },
                            { "preferredMadhab", userResponse.User.PreferredMadhab },
                        },
                        new[] { "total" }
                    );
                }
                else
                {
                    selectQuery = @"
                        MATCH (user:User)
                        WHERE 
                            (
                                ANY(scholar in user.favoriteIslamicScholars WHERE scholar IN $favoriteIslamicScholars) OR 
                                ANY(hobby in user.hobbies WHERE hobby IN $hobbies) OR
                                ANY(quranReciter in user.favoriteQuranReciters WHERE quranReciter IN $favoriteQuranReciters) OR
                                ANY(islamicStudyTopic in user.islamicStudyTopics WHERE islamicStudyTopic IN $islamicStudyTopics) OR
                                user.preferredMadhab = $preferredMadhab
                            ) AND  user.id <> $userId 
                        OPTIONAL MATCH (follower:User)-[:FOLLOWED]->(user)
                        OPTIONAL MATCH (user)-[:FOLLOW_USER]->(following:User)
                        RETURN user,
                                COLLECT(DISTINCT follower) AS followers,
                                COLLECT(DISTINCT following) AS following
                    ";

                    selectResult = await Neo4jHelpers.ReadAsync(
                        session,
                        $"{selectQuery} {pagingQuery}",
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "skip", (currentPage - 1) * itemsPerPage },
                            { "itemsPerPage", itemsPerPage },
                            { "favoriteIslamicScholars", userResponse.User.FavoriteIslamicScholars },
                            { "hobbies", userResponse.User.Hobbies },
                            { "islamicStudyTopics", userResponse.User.IslamicStudyTopics },
                            { "favoriteQuranReciters", userResponse.User.FavoriteQuranReciters },
                            { "preferredMadhab", userResponse.User.PreferredMadhab },
                        },
                        new[] { "user", "followers", "following" }
                    );

                    pagingResult = await Neo4jHelpers.ReadAsync(
                        session,
                        Neo4jHelpers.CommonCountCipher(selectQuery, "user"),
                        new Dictionary<string, object>
                        {
                            { "userId", userId },
                            { "favoriteIslamicScholars", userResponse.User.FavoriteIslamicScholars },
                            { "hobbies", userResponse.User.Hobbies },
                            { "islamicStudyTopics", userResponse.User.IslamicStudyTopics },
                            { "favoriteQuranReciters", userResponse.User.FavoriteQuranReciters },
                            { "preferredMadhab", userResponse.User.PreferredMadhab },
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

                usersToAdd = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(usersToAdd, pagination!));
        }

        /// <summary>
        /// Get message history items or message thread for a loggedin user.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="currentPage"></param>
        /// <param name="itemsPerPage"></param>
        /// <returns></returns>
        [HttpGet("{userId}/messages")]
        public async Task<IActionResult> Messages(
            string userId,
            [FromQuery] int currentPage = 1,
            [FromQuery] int itemsPerPage = 10
        )
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("You need to be logged in, in order to access your direct messages.");

            await using var session = _driver.AsyncSession();
            var messageHistoryItems = new List<Dictionary<string, object>>();
            Pagination? pagination = null;

            try
            {

                string pagingQuery = "SKIP $skip LIMIT $itemsPerPage";
                string selectQuery;

                List<Dictionary<string, object>>? selectResult;
                List<Dictionary<string, object>>? pagingResult;

                selectQuery = @"
                    // First match both users using parameters
                    MATCH (sender:User { id: $senderId })
                    MATCH (receiver:User)

                    // Find all messages between these users in either direction
                    MATCH (message:Message)
                    WHERE (message.senderId = sender.id AND message.recipientId = receiver.id)
                    OR (message.senderId = receiver.id AND message.recipientId = sender.id)

                    // Aggregate the results
                    WITH receiver, 
                        count(message) AS messageCount, 
                        max(message.createdAt) AS lastMessageTimestamp

                    // Return in the requested format
                    RETURN randomUUID() as id,
                    receiver.id as receiverId,
                    receiver.avatar as receiverProfileImage,
                    receiver.username as receiverUsername,
                    messageCount,
                    datetime(lastMessageTimestamp) as lastMessageDate
                ";

                selectResult = await Neo4jHelpers.ReadAsync(
                    session,
                    $"{selectQuery} {pagingQuery}",
                    new Dictionary<string, object>
                    {
                        { "senderId", userId },
                        { "skip", (currentPage - 1) * itemsPerPage },
                        { "itemsPerPage", itemsPerPage },
                    },
                    new[] { "id", "receiverId", "receiverProfileImage", "receiverUsername", "messageCount", "lastMessageDate" }
                );

                pagingResult = await Neo4jHelpers.ReadAsync(
                    session,
                    @"
                        // First match both users using parameters
                        MATCH (sender:User { id: $senderId })
                        MATCH (receiver:User)

                        // Find all messages between these users in either direction
                        MATCH (message:Message)
                        WHERE (message.senderId = sender.id AND message.recipientId = receiver.id)
                          OR (message.senderId = receiver.id AND message.recipientId = sender.id)

                        // Aggregate the results
                        WITH receiver, count(message) AS totalMessages, max(message.createdAt) AS lastMessageTimestamp

                        // Return in the requested format
                        RETURN count(receiver.id) as total
                    ",
                    new Dictionary<string, object>
                    {
                        { "senderId", userId },
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

                messageHistoryItems = selectResult ?? new List<Dictionary<string, object>>();
            }
            finally
            {
                await session.CloseAsync();
            }

            return Ok(new PaginatedResult<Dictionary<string, object>>(messageHistoryItems, pagination!));
        }

        /// <summary>
        /// Complete registration for a newly logged in user via social media provider.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        [HttpPost("{userId}")]
        public async Task<IActionResult> CompleteRegistration(
                [FromRoute] string userId,
                [FromBody] User.UserRegisterFormDto data)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("User ID is required to complete registration");
            }

            await using var session = _driver.AsyncSession();

            try
            {
                var listId = $"list_{Guid.NewGuid()}";
                var mutateCipher = @"
                  MATCH (u:User {id: $userId})
                  SET u.username = $username,
                      u.avatar = $avatar,
                      u.bgThumbnail = $bgThumbnail,
                      u.bio = $bio,
                      u.firstName = $firstName,
                      u.lastName = $lastName,
                      u.dateOfBirth = $dateOfBirth,
                      u.maritalStatus = $maritalStatus,
                      u.hobbies = $hobbies,
                      u.religion = $religion,
                      u.countryOfOrigin = $countryOfOrigin,
                      u.followingUsers = $followingUsers,
                      u.updatedAt = timestamp(),
                      u.isCompleted = true";


                await Neo4jHelpers.WriteAsync(
                    session,
                    mutateCipher,
                    new Dictionary<string, object>()
                    {
                        { "userId", userId },
                        { "bio", data.Bio },
                        { "avatar", data.Avatar },
                        { "bgThumbnail", data.BgThumbnail },
                        { "firstName", data.FirstName },
                        { "lastName", data.LastName },
                        { "dateOfBirth", data.DateOfBirth },
                        { "maritalStatus", data.MaritalStatus },
                        { "hobbies", data.Hobbies },
                        { "religion", data.Religion },
                        { "countryOfOrigin", data.CountryOfOrigin },
                        { "followjngUsers", data.FollowingUsers }
                    }
                );

                await Neo4jHelpers.WriteAsync(
                    session,
                    @"
                        MATCH (u:User {id: $userId})
                        CREATE (u)-[:NOTIFIED_BY]->(n:Notification {
                          id: ""notification_"" + randomUUID(),
                          message: ""You Completed your account registration."",
                          read: false,
                          relatedEntityId: u.id,
                          link: ""/users/"" + u.username,
                          createdAt: datetime(),
                          updatedAt: null,
                          _rev: null,
                          _type: ""notification"",
                          notificationType: ""your_account""
                        })
                    ",
                    new Dictionary<string, object>()
                    {
                        { "userId", userId }
                    }
                );

                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error completing registration: {err.Message}");
                return StatusCode(500, new { message = " completing registration error!", success = false });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

    }
}