# PRD: The AlSaqr Backend

**Date:** Wednesday, October 7, 2026
**Branch when written:** `feature/event-video`
**What this is:** a picture of the system as it is today.
It is not a plan for new work.

---

## The main point

AlSaqr is **1 server that runs 3 apps**: SocialMedia, Meetup, and Zook.
All 3 apps share **1 database**.

The server also runs 3 extra features:
**Yumna** (an AI helper), **Spaces** (live audio), and **event video** (being built now).

---

## 1. Why this exists

People use 3 apps with different jobs:
- **SocialMedia**: share posts and talk with others.
- **Meetup**: find groups and events near you, and meet local guides.
- **Zook**: buy and sell products near you.

Many things are shared between them: the same users, the same login, the same profile.
So 1 backend serves all 3.
This keeps user data in 1 place.

---

## 2. Who uses it

- **App users.** They reach the backend through the 3 apps. They never call it directly.
- **Logged-out visitors.** They can see some things, like cities, topics, and nearby events.
- **Group, event, and community owners.** They can edit, delete, and approve or remove members.
- **Local guides.** Meetup users who offer to show people around a city.
- **Sellers and buyers.** Zook users.
- **Developers and AI agents.** They build and change the backend.
  The rules for them are in `CLAUDE.md`.

---

## 3. What each app can do

These lists come from the code that exists today.
The official limits for each app are in [app-scope.md](app-scope.md).

### SocialMedia

Almost every action needs a logged-in user.

- **Sign in and session**: sign in with Supabase, and check the session.
- **Profiles**: see a profile, its posts, its media, and its groups, events, products, communities, and discussions.
- **Users**: edit or delete your account, finish sign-up, follow, and unfollow.
- **Posts**: see the feed, create, open, delete, like, repost, and bookmark posts. See comments.
- **Comments**: write a comment.
- **Bookmarks**: see your saved posts.
- **Lists**: create and delete lists. Save posts, comments, users, communities, discussions, and messages into them.
- **Communities**: create, edit, delete, join, and leave. Ask to join. Admins approve.
- **Community discussions**: the same as communities, plus messages inside each discussion.
- **Direct messages**: send messages and see your message threads.
- **Notifications**: see your notifications.
- **Explore**: read news from NewsAPI, by source.

### Meetup

Most lists are based on **where you are**.

- **Cities and topics**: pick lists for forms.
- **Groups**: find nearby groups and your groups. Create, edit, delete, join, and leave. Owners remove members. See details, members, and similar groups.
- **Events**: find nearby events, online events, and your events. Create, edit, delete, join, and leave. Owners remove attendees. See details, attendees, and nearby events.
- **Local guides**: find nearby guides. Create, edit, and delete a guide profile. See details.

### Zook

- **Categories**: see all product categories, or 1 category.
- **Products**: find nearby products, all of them or by category. Create, edit, and delete products.
- **Product details**: see a product and similar products.
- **Your products**: see what you sell and what you buy.

---

## 4. The extra features

### Yumna — the AI helper

- You send Yumna a message. It answers using **Google Gemini**.
- Each user can send **30 messages a day**. This depends on their subscription.
- After 30, the server says no until the next day.
- You can also check how many you used today.
- Spec: [yumna-ai-implementation.md](yumna-ai-implementation.md).

### Spaces — live audio rooms

- Like Twitter Spaces: a room where people talk live.
- A room belongs to a **community** or a **community discussion**.
- Users can start, join, leave, and end a room.
- Listeners **raise a hand**. The host can make them a speaker or move them back.
- Sound runs through **Cloudflare Calls**. Room updates go out through **Supabase Realtime**.
- A background job closes rooms that died without being ended.
- Spec: [audio-spaces.md](audio-spaces.md).

### Event video — being built now

- Live video for **online Meetup events**.
- Work is split into 2 plans:
  - **Plan A** (the groundwork) is done: the test project, the error handler fix, a real token check, and the CI tests.
  - **Plan B** (the video itself) has started. The data shapes (DTOs) are done.
- The plan uses **Galène** for video. The spec says **LiveKit**.
  The plan explains this change. *(see Open questions)*
- Spec: [video-streaming-online-events.md](video-streaming-online-events.md).
  Product PRD: [video-streaming-online-events.prd.md](video-streaming-online-events.prd.md).

---

## 5. How it works

### The big picture

```
 3 apps ──► AlSaqr API (.NET 8) ──► Supabase PostgreSQL (schema "alsaqr-2026")
                 │
                 ├──► Memory cache (on the server)
                 ├──► Supabase Realtime (live updates)
                 ├──► Cloudflare Calls (audio)
                 ├──► Google Gemini (Yumna)
                 └──► NewsAPI (Explore)
```

### The 4 parts of the code

| Part | What it holds |
|---|---|
| `AlSaqr.API` | The front door. Controllers, the error handler, background jobs. |
| `AlSaqr.Domain` | The shapes of data sent in and out (DTOs), and the error types. |
| `AlSaqr.Infrastructure` | The cache, token checks, and links to outside services. |
| `AlSaqr.Data` | Tables mapped to code (entities), and the code that reads and writes them (repositories). |

### How 1 request flows

1. A request comes into a **controller**.
2. The controller checks the login token.
3. It gets the logged-in user from the **user cache**.
4. For a read, it may answer from the **cache**.
5. If not, it asks a **repository**.
6. The repository talks to the database through the **Supabase client**.
7. If something fails, the repository throws an error type.
8. The **error handler** turns it into a standard error reply.

### Paged lists

- Long lists come back 1 page at a time.
- You ask with `currentPage`, `itemsPerPage`, and `searchTerm`.
- The total number of items comes from a database function.
- The order must be the same every time.

### Errors

| Error type | What the app gets |
|---|---|
| Not found | 404 |
| Bad input | 400 |
| Not allowed | 403 |
| Clashes with existing data | 409 |
| Anything else | 500, with no inside details |

Every error reply uses 1 standard format (RFC 7807 "ProblemDetails").

### Secrets

- In production, secrets come from **AWS Secrets Manager** when the server starts.
- They include keys for Supabase, Cloudflare, Gemini, NewsAPI, and the login tokens.

---

## 6. Rules the system must follow

The full rules are in `CLAUDE.md`. The most important ones:

- **Stale cache is the biggest risk.** Every change must clear the cached data it makes old.
- **Messages are never cached.** Not direct messages. Not discussion messages.
- **Lists always come back in the same order.**
- **Tests never use the shared database.** Each test run gets its own throwaway database (Testcontainers).
- **Only repositories talk to the database.**
- **Only the error handler turns errors into replies.**

---

## 7. Where things stand today

### What works well

- The error handler gives 1 standard error format. It is tested.
- A real token check (one that checks the signature) exists and is tested.
- Tests run on GitHub on every pull request.
- About 45 places use a database function for list totals, as the rules ask.

### Known problems

These come from reading the code on October 7, 2026.

1. **The logged-in user is shared by everyone.**
   The server stores "the logged-in user" under 1 key for the whole server.
   So when 2 people use it at once, 1 can act as the other.
   *(High risk. Needs a fix before more features depend on it.)*

2. **Most actions don't truly check who you are.**
   The usual token check only looks at the expiry date. It doesn't check the signature.
   The real check exists, but no controller uses it yet.

3. **Discussion messages are cached.**
   `CommunityDiscussionController` caches the first page of discussion messages.
   The rules say these are never cached.
   There is also cache code for direct message threads. Nothing calls it.

4. **Cache times are longer than the rules say.**
   The user cache keeps data for 30 minutes after last use. The rules say 60 seconds by default.

5. **Old code doesn't follow the newer rules yet.**
   - About 20 files still use `Newtonsoft.Json`.
   - About 43 `try/catch` blocks are in controllers.
   - The data moved off a Neo4j graph database. But `Neo4jHelper` is still in the code, and nothing calls it.

6. **The build has about 300 warnings.**
   So the `-warnaserror` build command in `CLAUDE.md` fails today.

7. **1 rule in `CLAUDE.md` can't be followed.**
   It asks for `[Table("name", Schema = "alsaqr-2026")]`.
   That doesn't compile. The schema is set once, in the Supabase settings.

8. **Few tests so far.**
   Tests cover the token check, the error handler, and the video data shapes.
   The main features of the 3 apps have no tests yet.

---

## 8. What is not part of this system

- The 3 apps' screens. They live in other projects.
- Anything outside [app-scope.md](app-scope.md) needs a clear decision before it is built.

---

## 9. Open questions

1. **Should `app-scope.md` list Yumna, Spaces, and event video?** Today it only lists the 3 apps.
2. **Galène or LiveKit for event video?** The plan chose Galène. The spec still says LiveKit.
3. **How should the logged-in user be found?** Should it come from the checked token on each request, not a shared cache?
4. **Should `CLAUDE.md` be fixed?** For the `Schema` rule and the `-warnaserror` command.
5. **Can the unused code be removed?** That is `Neo4jHelper` and the direct-message cache code.

---

## Words to know

- **Backend**: the server part. The apps talk to it. Users don't see it.
- **API**: the set of web addresses the apps call to get or change data.
- **Supabase**: the hosted service that runs our PostgreSQL database.
- **Cache**: a fast short-term copy of data, kept in the server's memory.
- **Stale cache**: a cached copy that is out of date.
- **Controller**: code that takes a web request and sends back a reply.
- **Repository**: code that reads and writes 1 kind of data in the database.
- **Entity**: a database table, written as code.
- **DTO**: the shape of data sent to or from the apps.
- **Token**: a code the app sends to prove you are logged in.
- **Signature**: the part of a token that proves the server made it.
- **Testcontainers**: a tool that starts a fresh, throwaway database for tests.
- **CI**: tests that run on their own on GitHub.
- **SFU**: a server that passes live audio or video between people. Cloudflare Calls and Galène are SFUs.
