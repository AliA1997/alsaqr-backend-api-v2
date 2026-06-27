# Specification: Profile — Integrate Access Token logic


## Overview

When a user is upserting an event or group, it would check if the user is logged in, and their access token is not expired via the Bearer Token passed into the header.

1. Access token accessed from request header.
2. If it exists continue, else check the expiration. If it's not expired, then continue.

This will replace all logged in logic such as this:
``` csharp
            var loggedInUser = _userCacheService.GetLoggedInUser();

            if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
                return Unauthorized("User must be logged in to create a post.");

            if (loggedInUser.Id == Guid.Empty)
            {
                return BadRequest("User ID is required");
            }
``` 
with logic checking access token passed in, and expiration date.


## Acceptance
When creating a new group if an access token is not provided, it should not continue the request. 
When creating a new group if an access token is expired, it should not continue the request.


## Out of scope (deferred to later phases)
Don't replace all functionality, this just applied to groups, events, and localguides controllers.

