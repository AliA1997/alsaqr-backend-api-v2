# Overview
- Next stage of changes that should be applied in july 4th 2026
- First set of changes is allowing users to join groups. 
- Users can join events if they are part of the group. If they are not, let them know to join the group first.
- Group founder can remove people from their group, or just remove them from events.
- Allow web3 login into the auth flow, also integrate a web3 boolean value into the session. Since web3 users would display differently compared to normal oauth users. 
- Refactor SessionController into AuthController or Remove AuthController completely this would be upto the ai agent.

## Implementation Steps
1) Generate Data Models that would exist in the AlSaqr.Data/Meetup/* directory. Two, one for GroupMember db class, and EventMember db class. Similar to community member.
2) Generate corresponding postgresql tables for these two new data models
3) Add a new web3 Address property to the AlSaqr data modal class that exists in the AlSaqr.Data/Meetup* directory.
4) Generate corresponding postgresql db scripts for it.
5) Create a custom exception wrapper in the Meetup/* directory similar to AlSaqr/SocialMedia custom exceptions.
6) Create new exception class that inherit from Exception.Common class, called JoinGroupException, JoinEventException, UnjoinGroupException, and UnjoinEventException
7) Update the Group controller, and add a new put endpoint that would add a new user to the group based on the logged in user, user's id.

8) Update the Event controller, and add a new put endpoint that would add a new user to the event based on the logged in user, user's id.
9) Add an endpoint for removing group members in the Group Controller -> Must be group owner.
10) Add an endpoint for removing event members in the Event Controller -> Must be group owner.
11) Update check and sign in endpoint to set, and retrieve the web3 address. 
12) Update the complete registration endpoint to the record the web3 address, and check if their email already exists. If it does already exists, just update the existing record web3 address. 
13) Move the check and sign in endpoint to the AuthController, if it makes sense. 

## Rules 
1) Logged in users that pass in a access token can join any group. group_member db table would be updated.

2) Logged in users that pass in an access token can join any event, when they join an event, by default they join any group. Would update group_member if they are not part of the group. Would update event_member db table.
3) A group founder can delete a member from event or group, therefore it would delete from group_member in the case of delete member from group. It would delete from event_member in cases when they delete a member from a event.
4) When the user signs in with a web address, it would pass the web3 address. It would then check it against the user db table, if it exists, then it would get the existing db record similar to email, else it would create a new record.
5) User would use the auth controller to sign in, and the session controller to check the session.


## Acceptance
Passed
- An logged in user goes to a group page, and joins the group. His user id, and the group id he wants to join that results in a record existing in the group_member db table.
- An logged in user goes to a event page, and joins the event. His user id, and the group id he wants to join that results in a record existing in the group_member db table, and event id would have an existing record in the event_member table.
- A user logins with web3 address, and put an email that doesn't exist in db, which results a new user record being completed.
- A user logins with web3 address, puts an email that does exist in db, would just update the web3address fieid in the user record.
- A group founder remove user from event, it just remove the user from the event, not the group.
Fail
- An logged in user doesn't pass in a access token, and wants to join group. It should return an UnAuthorized status code.
- An logged in user doesn't pass in a access token, and wants to join event. It should return an UnAuthorized status code.
- An logged in user doesn't pass in a access token, and wants to join group. It should return an UnAuthorized status code.
- An logged in user doesn't pass in a access token, and wants to join event. It should return an UnAuthorized status code.
- A random user tries to remove a member from an event, it should return a 500 status code error.


## Out of Scope
- Don't create a new pattern, use existing patterns.


## Reference Code
1) Access Token Check
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
3) Reference code for checking if the user is the founder of the group.
```csharp
// Check if the community to update is the founder, if it isn't return an exception.
Community? communityToUpdate = (await supabase.From<Community>()
                                            .Where(c => c.FounderId == userId && c.Id == communityId).Single());
if (communityToUpdate == null)
    throw new Exception("Can't update the community");
```
4) Reference Code for creating custom exceptions
```csharp
using AlSaqr.Domain.Common;

namespace AlSaqr.Domain.Zook.Exceptions
{
    public class UpdateProductException : PutException
    {
        public Guid ProductId { get; }

        public UpdateProductException(Guid productId)
            : base($"Failed to update product with ID: {productId}.")
        {
            ProductId = productId;
        }

        public UpdateProductException(Guid productId, Exception innerException)
            : base($"Failed to update product with ID: {productId}.", innerException)
        {
            ProductId = productId;
        }
    }
}
```
5) Reference code for defining the GroupMember, and EventMember base class:
```csharp
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace AlSaqr.Data.Entities.SocialMedia
{
    [Table("community_members")]
    public class CommunityMember : BaseModel
    {
        [PrimaryKey("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Column("community_id")]
        public Guid CommunityId { get; set; }
        [Column("user_id")]
        public Guid UserId { get; set; }
        [Column("role")]
        public string Role { get; set; } = "member";
        [Column("joined_at")]
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    }
}
```
6) Reference code for defining the repository for add new group/event member, and removing them as an admin.
```csharp
using AlSaqr.Data.Entities.SocialMedia;
using AlSaqr.Data.Repositories.SocialMedia.Impl;
using AlSaqr.Domain.SocialMedia.Exceptions;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using static Supabase.Postgrest.QueryOptions;

namespace AlSaqr.Data.Repositories.SocialMedia
{
    public class CommunityMemberRepository: ICommunityMemberRepository
    {

        public CommunityMemberRepository() { }


        // Membership roles. The Neo4j relationship types (JOINED / INVITED /
        // INVITE_REQUESTED) collapse onto a single CommunityMember row that is
        // distinguished by its Role value.
        private const string RoleMember = "member";
        private const string RoleInvited = "invited";
        private const string RoleRequested = "requested";

        
        public async Task JoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct)
        {
            try
            {
                // Upsert the membership row as a full member.
                var member = new CommunityMember
                {
                    CommunityId = communityId,
                    UserId = userId,
                    Role = RoleMember,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityMember>()
                    .Insert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} joined your community of {community}.",
                    notificationType: "user_joined",
                    ct
                );

            }
            catch(JoinCommunityException ex) 
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new JoinCommunityException(communityId, ex);
            }
        }

        public async Task UnJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId, 
            CancellationToken ct)
        {
            try 
            {

                // Delete the "user_joined" notification on the founder's feed.
                var community = await supabase
                    .From<Community>()
                    .Where(c => c.Id == communityId)
                    .Single(ct);

                if (community != null)
                {
                    await supabase.From<Notification>()
                        .Where(x => x.RelatedUserId == userId && x.CommunityId == communityId)
                        .Delete(null, ct);

                    var unjoinedUser = await supabase
                            .From<AlSaqrUser>()
                            .Where(u => u.Id == userId)
                            .Single(ct);

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: userId,
                        communityId: communityId,
                        messageTemplate: $"Someone with ID of {unjoinedUser?.Username} has unjoined your community of {community}.",
                        notificationType: "user_unjoined",
                        ct
                    );
                }

                // Remove the membership row regardless of its role
                // (covers both JOINED and INVITED states from Neo4j).
                await supabase
                    .From<CommunityMember>()
                    .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                    .Delete(null, ct);

            }
            catch(UnJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new UnJoinCommunityException(communityId, ex);
            }
        }

        public async Task RequestJoinCommunity(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            CancellationToken ct)
        {
            try 
            {
                var member = new CommunityMember
                {
                    CommunityId = communityId,
                    UserId = userId,
                    Role = RoleRequested,
                    JoinedAt = DateTime.UtcNow,
                };

                await supabase
                    .From<CommunityMember>()
                    .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);

                await CreateCommunityMemberNotification(
                    supabase,
                    userId: userId,
                    communityId: communityId,
                    messageTemplate: "{username} has requested to join your community of {community}.",
                    notificationType: "user_request_join",
                    ct
                );
            }
            catch(RequestToJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RequestToJoinCommunityException(communityId, ex);
            }

        }


        public async Task RespondToJoinRequest(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            bool accept, 
            CancellationToken ct)
        {
            try
            {
                if (accept)
                {
                    // Promote the pending request row to an invited/member row.
                    var existing = await supabase
                        .From<CommunityMember>()
                        .Where(cm => cm.UserId == userId)
                        .Where(cm =>  cm.CommunityId == communityId)
                        .Single(ct);

                    if (existing != null)
                    {
                        existing.Role = RoleInvited;
                        existing.JoinedAt = DateTime.UtcNow;

                        await supabase
                            .From<CommunityMember>()
                            .Where(cm => cm.Id == existing.Id)
                            .Upsert(existing, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }
                    else
                    {
                        // No pending request found — create the invited row directly.
                        var member = new CommunityMember
                        {
                            CommunityId = communityId,
                            UserId = userId,
                            Role = RoleInvited,
                            JoinedAt = DateTime.UtcNow,
                        };

                        await supabase
                            .From<CommunityMember>()
                            .Upsert(member, new QueryOptions { Returning = ReturnType.Minimal }, ct);
                    }

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: userId,
                        communityId: communityId,
                        messageTemplate: "{username} invited to  your community of {community}.",
                        notificationType: "user_joined",
                        ct
                    );
                }
                else
                {
                    // Deny: remove the pending request row.
                    await supabase
                        .From<CommunityMember>()
                        .Where(cm => cm.UserId == userId && cm.CommunityId == communityId)
                        .Filter("role", Operator.Equals, RoleRequested)
                        .Delete(null, ct);

                    await CreateCommunityMemberNotification(
                        supabase,
                        userId: userId,
                        communityId: communityId,
                        messageTemplate: "{username} denied from your community of {community}.",
                        notificationType: "user_denied",
                        ct
                    );
                }

                // In both branches, delete the original "user_request_join" notification.
                var community = await supabase
                    .From<Community>()
                    .Where(c => c.Id == communityId)
                    .Single(ct);

                if (community != null)
                {
                    await supabase
                        .From<Notification>()
                        .Where(n => n.UserId == community.FounderId)
                        .Where(n => n.CommunityId == communityId)
                        .Where(n => n.NotificationType == "user_request_join")
                        .Delete(null, ct);
                }

            }
            catch (RespondToRequestToJoinCommunityException ex)
            {
                throw ex;
            }
            catch(Exception ex)
            {
                throw new RespondToRequestToJoinCommunityException(communityId, ex);
            }
        }


        private async Task CreateCommunityMemberNotification(
            Supabase.Client supabase,
            Guid userId,
            Guid communityId,
            string messageTemplate,
            string notificationType, 
            CancellationToken ct = default)
        {
            var community = await supabase
                .From<Community>()
                .Where(c => c.Id == communityId)
                .Single();

            if (community == null || community.FounderId == userId)
                return;

            var newCommunityMember = await supabase
                .From<AlSaqrUser>()
                .Where(u => u.Id == userId)
                .Single();

            var communityMemberName = newCommunityMember?.Username ?? "Someone";


            var message = messageTemplate
                .Replace("{username}", communityMemberName)
                .Replace("{community}", community.Name);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = community.FounderId,
                Read = false,
                CreatedAt = DateTime.UtcNow,
                Message = message,
                NotificationType = notificationType,
                ItemType = "community",
                RelatedUserId = userId,
                CommunityId = communityId,
                Link = $"/users/{communityMemberName}",
            };

            var created = await supabase
                .From<Notification>()
                .Insert(notification, new QueryOptions { Returning = ReturnType.Representation }, ct);

            if (created == null)
                throw new Exception("Error creating notification");
        }
    }
}
```
7) Reference code for passing web3Address to the check.
```csharp
/// <summary>
/// Create a comment
/// </summary>
/// <param name="request"></param>
/// <returns></returns>
[HttpPost]
public async Task<IActionResult> CreateComment(
    [FromBody] AlSaqrUpsertRequest<Posts.CreateCommentDto> request,
    [FromQuery] bool onComment = false)
{

    var authError = ValidateAccessToken();
    if (authError != null)
        return authError;

    var data = request.Values;
    var loggedInUser = _userCacheService.GetLoggedInUser();
    Guid.TryParse(loggedInUser?.Id?.ToString(), out var userId);

    if (string.IsNullOrEmpty(data?.Text))
        return BadRequest("Text of the Comment is required");
    if (data.PostId == Guid.Empty)
        return BadRequest("Post Id is required to create a comment.");

    var cts = new CancellationTokenSource();
    var ct = cts.Token;
    await _commentsRepository.CreateComment(
        _supabase,
        userId,
        data.PostId,
        data,
        ct
    );

    _logger.LogInformation("Comment created Successfully for Post {postId}", data.PostId);
    _socialMediaCacheService.ClearInitialComments(data.PostId);

    return Ok(new { success = true });
    
}
```
8) Original Coe for session check:
```csharp
      /// <summary>
        /// Check user if he's logged in.
        /// </summary>
        /// <returns></returns>
        [HttpPost("check")]
        public async Task<IActionResult> Check(
            [FromBody] Common.AlSaqrUpsertRequest<SessionCheckRequest> request
        )
        {
            var data = request.Values;
            // Input validation
            if (string.IsNullOrEmpty(data.Email))
            {
                return BadRequest("Enail is required");
            }
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                var (userId, username) = await _userRepository.GetUserIdAndUsernameByEmail(
                    _supabase,
                    data.Email
                );

                var sessionUserResult = await _profileRepository.GetSessionInfo(_supabase, userId);

                _logger.LogInformation("User signed in successfully!");

                if (sessionUserResult.Id == Guid.Empty || sessionUserResult.Id == null)
                    return BadRequest("Invalid user retrieved");

                _userCacheService.SetLoggedInUser(sessionUserResult);

                return Ok(new { result = sessionUserResult });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Fetch User Session error!");
                return StatusCode(
                    500,
                    new { message = "Fetch User Session error!", success = false }
                );
            }
        }
```
10) Auth Controller code, IMPORTANT NOTE: alot of this code is dead.
```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AlSaqr.API.Controllers.SocialMedia
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {

        private readonly ILogger<AuthController> _logger;


        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
        }

        [HttpGet("external-login/{provider}")]
        public IActionResult ExternalLogin([FromRoute] string provider, [FromQuery] string returnUrl = "/")
        {
            var redirectUrl = Url.Action(nameof(ExternalCallback), "Auth", new { returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet("signin-google")]
        public IActionResult GoogleSignin()
        {

            return Ok();
        }

        [HttpGet("external-callback")]
        public async Task<IActionResult> ExternalCallback(string returnUrl = "/")
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
                return BadRequest("External authentication error");

            var claims = authenticateResult.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            // (Optional) register user or generate JWT here

            return Redirect($"http://localhost:3000{returnUrl}?email={email}&name={name}");
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                User.Identity?.Name,
                Email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
            });
        }
    }
}

```