# Specification: Integrate Access Token logic

## Overview

Whenever a user upserts an entity, the request MUST carry a valid (unexpired) Bearer
access token in the `Authorization` header. This replaces the old "is the user logged in?"
check.

This applies to **every upsert across the project**, including:

- User
- Post
- Community
- Community discussion
- Community discussion message
- Message
- Group, Event, Local guide (already implemented)

1. Read the access token from the `Authorization` request header.
2. If it is missing or expired, reject the request. Otherwise, continue.

## Rules

1. Any controller with an upsert (POST / PUT / PATCH / DELETE) action MUST derive from
   `AuthorizedControllerBase` and call `ValidateAccessToken()` at the top of that action.
2. Replace logged-in checks like the one below with the access-token check:

   ```csharp
   var loggedInUser = _userCacheService.GetLoggedInUser();
   if (loggedInUser == null || loggedInUser.Id == Guid.Empty)
       return Unauthorized("User must be logged in to create a post.");
   ```

   with:

   ```csharp
   var authError = ValidateAccessToken();
   if (authError != null)
       return authError;
   ```

   Note: the access token gates the request; the acting user's identity still comes from
   the user cache where an action needs it.

## Acceptance

- Upserting an entity without an access token MUST NOT continue the request.
- Upserting an entity with an expired access token MUST NOT continue the request.

## Out of scope

- Read-only (GET) endpoints.
- Signature verification of the token (the gate only checks presence and expiration).
