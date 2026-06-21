# Specification: Profile — Communities, Discussions & Products

> **SDD Phase 1 — Specification (the WHAT / WHY).**
> This document describes observable behavior only. It names no endpoints,
> controllers, repositories, entities, tables, or columns — those belong to
> Technical Planning (`plan.md`) and Task Breakdown (`tasks.md`).
> Conformance keywords (**MUST**, **MUST NOT**, **SHOULD**, **MAY**) follow RFC 2119.
>
> This spec is a sibling of *Profile — Joined Groups & Attended Events*. All three
> collections here are **profile-scoped**: they list items belonging to the user
> whose profile is being viewed.

---

## Overview

Three read-only, paginated, name-searchable, recency-ordered collections, each
**scoped to a single profile** (the user being viewed):

1. **Communities** — the communities that profile is associated with, annotated
   with the **requesting viewer's** role/relationship to each one.
2. **Community discussions** — the discussions that profile is associated with,
   annotated with the requesting viewer's role/relationship to each one.
3. **Products** — the products belonging to that profile.

> **Two users are in play.** The collection is *scoped* by the **profile owner**
> (the user being viewed). Where items are role-annotated, the role reflects the
> **requesting viewer** (the signed-in user doing the browsing). These can be the
> same person or different people.

> **Relationship to existing methods.** The new profile-scoped repository methods
> follow the same pattern as the platform's existing *global* community browse and
> *by-community* discussion browse, which remain in place for their own contexts.
> Those siblings use their own count sources (`get_all_community_count`,
> `get_all_community_discussions_count`); the profile-scoped methods specified here
> use the profile count functions named in the Data Contract.

---

## Requirements — Profile Communities

### COMM-1 — Retrieve a profile's communities
A client can request the communities associated with a given profile, addressed by
that profile's user.

**Acceptance**
- The response contains only the communities associated with that profile.
- A community the profile is not associated with never appears.

### COMM-2 — Community entry contents (viewer-personalized)
Each returned community **MUST** include the community's identity and metadata as
defined by its detail view, **and** the **requesting viewer's** role/relationship
to that community.

**Acceptance**
- The same community, requested by two different viewers, carries each viewer's own
  role/relationship — role data is a function of who is asking, not a fixed property
  of the community.
- A viewer with no relationship to a community still sees it, with a
  role/relationship value indicating "none."

> *Open: confirm whether role annotation keys off the requesting viewer or the
> profile owner. The existing global browse annotates for the requesting viewer;
> this spec assumes the same.*

### COMM-3 — Ordering
Communities **MUST** be ordered by creation time, **most recently created first**.

**Acceptance**
- Given communities created on distinct dates, the response lists them newest → oldest.

### COMM-4 — Pagination
The collection **MUST** be returned using the platform's standard pagination.

**Acceptance**
- A page of size *N* returns at most *N* communities.
- Consecutive pages contain no duplicated and no skipped communities.

### COMM-5 — Filter by community name
The collection **MAY** be narrowed by an optional search term that matches the
**community name** case-insensitively, as a substring. When no search term is
supplied, all of the profile's communities are returned.

**Acceptance**
- Given a search term, only communities whose name contains it (case-insensitive) appear.
- Pagination totals (item count, page count) reflect the **filtered** set, not the full set.
- Omitting the search term returns the profile's full community collection.

### COMM-6 — Empty collection
A profile associated with no communities — or whose communities don't match an
active search term — **MUST** yield an empty, well-formed page rather than an error.

**Acceptance**
- A profile with zero communities returns an empty, well-formed page.
- A search term matching no community returns an empty, well-formed page.

> *Open: behavior for an unknown profile follows the groups/events precedent — if
> the profile is resolved by username via a single-record lookup, an unknown
> username raises rather than returning an empty page. Confirm the resolution path.*

---

## Requirements — Profile Community Discussions

### DISC-1 — Retrieve a profile's discussions
A client can request the community discussions associated with a given profile,
addressed by that profile's user. (This is keyed by the **profile**, not by a single
community.)

**Acceptance**
- The response contains only discussions associated with that profile.

### DISC-2 — Discussion entry contents (viewer-personalized)
Each returned discussion **MUST** include the discussion's identity and metadata as
defined by its detail view, **and** the **requesting viewer's** role/relationship
to it (carried the same way as COMM-2).

**Acceptance**
- The same discussion, requested by two different viewers, carries each viewer's
  own role/relationship.

### DISC-3 — Ordering
Discussions **MUST** be ordered by creation time, **most recently created first**.

**Acceptance**
- Given discussions created on distinct dates, the response lists them newest → oldest.

### DISC-4 — Pagination
The collection **MUST** be returned using the platform's standard pagination
(same guarantees as COMM-4).

### DISC-5 — Filter by discussion title
The collection **MAY** be narrowed by an optional search term that matches the
**discussion title** case-insensitively, as a substring. When no search term is
supplied, all of the profile's discussions are returned.

**Acceptance**
- Given a search term, only discussions whose title contains it (case-insensitive) appear.
- Pagination totals reflect the **filtered** set.
- Omitting the search term returns the profile's full discussion collection.

### DISC-6 — Empty collection
A profile associated with no discussions — or whose discussions don't match an
active search term — **MUST** yield an empty, well-formed page rather than an error.

**Acceptance**
- A profile with zero discussions returns an empty, well-formed page.
- A search term matching no discussion returns an empty, well-formed page.

---

## Requirements — Profile Products

### PROD-1 — Retrieve a profile's products
A client can request the products belonging to a given profile, addressed by that
profile's user.

**Acceptance**
- The response contains only that profile's products.

### PROD-2 — Product entry contents
Each returned product **MUST** include the product's identity and metadata as
defined by the `Products` record.

> *Open: whether products are also viewer-personalized with a role/relationship
> (as communities and discussions are) is unknown — the community/discussion code
> annotates via a role-assignment step; if products have no equivalent, this
> requirement carries no viewer-personalization. Confirm.*

### PROD-3 — Ordering
Products **SHOULD** be ordered by creation time, most recently created first
*(inferred to match COMM-3 / DISC-3 — confirm the actual ordering column)*.

### PROD-4 — Pagination
The collection **MUST** be returned using the platform's standard pagination
(same guarantees as COMM-4).

### PROD-5 — Filter by product name
The collection **MAY** be narrowed by an optional search term matching the
**product name** case-insensitively, as a substring *(inferred search field —
confirm)*. When no search term is supplied, the full collection is returned.

**Acceptance**
- Pagination totals reflect the **filtered** set.

### PROD-6 — Empty collection
A profile with no products, or a search term with no hits, **MUST** yield an empty,
well-formed page rather than an error.

---

## Out of scope (deferred to later phases)

Decided in Technical Planning and Task Breakdown, not here:
- route shapes and where each endpoint hangs off the profile surface;
- repository method signatures and the read-model (`Vw…`) entity classes;
- the request/response DTO split in the domain layer;
- the concrete pagination mechanism and parameters;
- the mechanism by which a viewer's roles are attached to each item;
- how the profile is resolved (username vs. user id) — see COMM-6's open note.

---

## Data Contract (canonical source of truth)

Each collection's exact record shape is defined by its detail view, **except
products, which are sourced directly from the `Products` table.** The community
view DDL is not yet attached — paste it in to lock the field-level contract (the
way the groups/events spec reproduces `vw_group_attendees` and `vw_event_attendees`);
for products, the `Products` table definition is the contract.

Profile-scoped totals come from these count functions. Both take
`p_user_id UUID` (the profile owner) and `p_search_term TEXT` (the optional name/title
filter; null/empty means no filter):

| Collection | Read-model entity | Detail view (expected) | Profile count function |
|---|---|---|---|
| Communities | `VwCommunityDetails` | `vw_community_details` | `get_profile_community_count(p_user_id, p_search_term)` |
| Community discussions | `VwCommunityDiscussionDetails` | `vw_community_discussion_details` | `get_profile_community_discussions_count(p_user_id, p_search_term)` |
| Products | `Products` | none — sourced directly from the `Products` table | *unknown — provide* |

---

## Traceability stub (for Phase 2)

Every `plan.md` item must trace back to a requirement ID below.

| Requirement | Planning concern |
|---|---|
| COMM-1, DISC-1, PROD-1 | profile-scoped query keyed by the profile's user |
| COMM-2, DISC-2 | viewer-role annotation step applied to each item |
| COMM-3, DISC-3, PROD-3 | `…_created_at` descending order on the query |
| COMM-4, DISC-4, PROD-4 | standard pagination utility + filtered count source |
| COMM-5, DISC-5, PROD-5 | optional name/title search applied to query **and** count so totals stay consistent |
| COMM-6, DISC-6, PROD-6 | empty/zero-count handling; unknown-profile behavior per resolution path |
| COMM-5, COMM-6 | `get_profile_community_count(p_user_id, p_search_term)` as the count source |
| DISC-5, DISC-6 | `get_profile_community_discussions_count(p_user_id, p_search_term)` as the count source |