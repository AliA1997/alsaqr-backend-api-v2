# Specification: Profile — Integrate Access Token logic


## Overview

Add upsert, and delete functionality to events, groups, and local guides.
For Groups, make sure the it's the group founder that can delete groups.
For Events, make sure that it's the groups founder-> since events are children of groups, can delete events.
For local guide, they can unregister(delete the local guide record) if they are current user id.

1. Add to the EventRepository, GroupRepository, and LocalGuideRepository create, update, and delete functionality. 
2. Create a http post, put, and delete endpoints. 
3. Use the corresponding repository to perform create, update, and delete functionality.

Add :
``` csharp
        /// <summary>
        /// Create a post
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] AlSaqrUpsertRequest<Posts.CreatePostDto> request)
        {
            var data = request.Values;
            var loggedInUser = _userCacheService.GetLoggedInUser();
            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to create a post.");

            if (loggedInUser.Id == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }
            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            if (string.IsNullOrEmpty(data?.Text))
            {
                return BadRequest("Text of the Post is required");
            }

            
            await _postRepository.CreatePost(_supabase, userId, data, ct);

            _socialMediaCacheService.ClearInitialPosts(userId, 0);

            return Ok(new { success = true });
            
        }
        /// <summary>
        /// Update user based on user id.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>

        [HttpPut]
        public async Task<IActionResult> UpdateUser([FromBody] AlSaqrUpsertRequest<User.UpdateUserDto> request)
        {
            var data = request.Values;
            using var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to update their user.");

            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);
            try
            {
                await _userRepository.UpdateUser(_supabase, userId, data, ct);
                var sessionUserResult = await _profileRepository.GetSessionInfo(_supabase, userId);
                _userCacheService.SetLoggedInUser(sessionUserResult);

                return Ok(new { succcess = true });
            }
            catch (Exception err)
            {
                // Log the exception here
                Console.WriteLine($"Error updating user: {err.Message}");
                return StatusCode(500, new { message = "Update user error!", success = false });
            }
        }

        /// <summary>
        /// Delete user based on logged in user.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        public async Task<IActionResult> DeleteUser()
        {
            var loggedInUser = _userCacheService.GetLoggedInUser();
            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to delete their user.");

            Guid.TryParse(loggedInUser.Id.ToString(), out var userId);

            try
            {
                await _userRepository.DeleteUser(_supabase, userId);
                return Ok(new { success = true });
            }
            catch (Exception err)
            {
                _logger.LogError(err, "Delete user error!");
                return StatusCode(500, new { message = "Delete user error!", success = false });
            }
        }

``` 
Example reference code for user that serves an example for updating groups, localguide, and events.


## Acceptance
If a user is trying all this functionality, they need a valid access token, if they don't return UnAuthorized exception.
When creating a new group, create a new notification with a type of 'group_created'
When creating a new event, create a new notification with a type of 'event_created'
When creating a new local guide, create a new notification with a type of 'local_guide_created'.
When updating a new group, create a new notification with a type of 'group_updated'
When updating a new event, create a new notification with a type of 'event_updated'
When updating a new local guide, create a new notification with a type of 'local_guide_updated'.
When creating a new group, create a new notification with a type of 'group_created'
When creating a new event, create a new notification with a type of 'event_created'
When creating a new local guide, create a new notification with a type of 'local_guide_created'.
When deleting a new group, create a new notification with a type of 'group_deleted'
When deleting a new event, create a new notification with a type of 'event_deleted'
When deleting a new local guide, create a new notification with a type of 'local_guide_deleted'.

## Reusable access-token validation

The per-controller `private IActionResult? ValidateAccessToken()` was duplicated across
`EventsController`, `GroupsController`, and `LocalGuidesController`. It has been factored
into a single shared base controller:

- `AlSaqr.API/Controllers/AuthorizedControllerBase.cs` — `abstract class
  AuthorizedControllerBase : ControllerBase` exposing `protected IActionResult?
  ValidateAccessToken()`.
- The three meetup controllers now inherit from `AuthorizedControllerBase` instead of
  `ControllerBase`; their local copies of the method were removed. Call sites are
  unchanged (`var authError = ValidateAccessToken(); if (authError != null) return authError;`).
- Performance: the shared method reads `Request.Headers.Authorization` (the typed
  `StringValues` accessor) rather than the string indexer `Request.Headers["Authorization"]`,
  and `Auth.AccessTokenValidator.IsValid` only base64-decodes the JWT payload to check the
  `exp` claim — no signature verification (see specs/access-token.md).
- Any future controller that needs the access-token gate MUST derive from
  `AuthorizedControllerBase` rather than re-declaring the method.

## Cities dropdown endpoint

A `CitiesController` provides cities for front-end dropdown selection.

- `GET /api/Cities` → `200 OK` with a flat list of `CityDto`.
- Returns up to **100 distinct** cities (distinct by name). **Not paginated** — this is
  global reference data, not user-scoped, so the §4.1 user-scoped caching rule does not
  apply and pagination is intentionally omitted.
- Ordering is deterministic: `name` ascending, then `id` ascending as a tie-breaker
  (per §3.2).
- DTO (`AlSaqr.Domain/Meetup/CityDto.cs`), serialized camelCase to match the front-end
  `City` interface `{ id, name, stateOrProvince?, country, latitude?, longitude? }`. Note:
  the `cities` table keys on a `Guid`, so `id` serializes as a GUID string rather than a
  numeric value; the front-end `id: number` should be treated as an opaque identifier.
- Repository: `ICityRepository.GetCities(Supabase.Client client)` /
  `CityRepository.GetCities` — queries the `City` entity through the Supabase PostgREST
  client (the controller never touches the client beyond passing it in) and maps rows to
  `CityDto`. `CityRepository` is already registered in DI (`Program.cs`).

## Out of scope (deferred to later phases)
Don't add any new nuget packages, follow common patterns found in the userrepository

