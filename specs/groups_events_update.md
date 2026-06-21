# Specification: Profile — Joined Groups & Attended Events

> **SDD Phase 1 — Specification (the WHAT / WHY).**
> This document describes observable behavior only. It names no endpoints,
> controllers, repositories, entities, tables, or columns — those belong to
> Technical Planning (`plan.md`) and Task Breakdown (`tasks.md`).
> Conformance keywords (**MUST**, **MUST NOT**, **SHOULD**, **MAY**) follow RFC 2119.
>
> The PostgreSQL view definitions are reproduced in the **Data Contract**
> appendix as the canonical source of truth for the shape of each record.
> They are reference material, not requirements.

---

## Overview

A visitor viewing a user's profile can browse two collections belonging to that
user, addressed by the profile's **username**:

1. the **groups the user has joined**, and
2. the **events the user attended**.

Both collections are read-only, paginated, ordered by recency of the user's
involvement, and narrowable by an optional name search. Neither collection
requires the viewer to be the profile owner.

---

## Requirements — Joined Groups

### GJ-1 — Retrieve a profile's joined groups
A client can request the collection of groups a given user has joined, identified
by that user's **username**.

**Acceptance**
- Given an existing username who has joined one or more groups, the response
  contains exactly those groups and no others.
- A group the user has *not* joined never appears.

### GJ-2 — Group entry contents
Each returned group **MUST** include:
- the group's identity: **name**, **description**, **images**, and **slug**;
- the group's headquarters city: **city name**, **country**, **latitude**, **longitude**;
- the **topics** associated with the group, each carrying its **id** and **name**;
- the **date/time the user joined** the group.

**Acceptance**
- A group with two topics returns both topics.
- A group with no topics returns an **empty** topic list, never a null/absent value.

### GJ-3 — Ordering
Joined groups **MUST** be ordered by join time, **most recently joined first**.

**Acceptance**
- Given groups joined on three distinct dates, the response lists them newest → oldest.

### GJ-4 — Pagination
The collection **MUST** be returned using the platform's standard pagination, so a
client can page through an arbitrarily large set without retrieving it all at once.

**Acceptance**
- Requesting a page of size *N* returns at most *N* groups.
- Consecutive pages contain no duplicated and no skipped groups.

### GJ-5 — Empty collection
A user who exists but has joined no groups — or whose joined groups don't match an
active search term — **MUST** yield an empty, well-formed page rather than an error.

**Acceptance**
- A user with zero joined groups returns an empty, well-formed page.
- A search term matching none of the user's joined groups returns an empty, well-formed page.

> **Unknown profile is an error, not an empty page.** Resolving the profile by
> username is a precondition: if the username does not exist, the request fails
> (the profile lookup raises), it does **not** return an empty page. (This differs
> from the earlier draft of this spec, which incorrectly promised a graceful empty
> page for unknown usernames.)

### GJ-6 — Filter by group name
The collection **MAY** be narrowed by an optional search term that matches the
**group name** case-insensitively, as a substring. When no search term is supplied,
all of the user's joined groups are returned.

**Acceptance**
- Given a search term, only groups whose name contains it (case-insensitive) appear.
- Pagination totals (item count, page count) reflect the **filtered** set, not the full set.
- Omitting the search term returns the user's full joined-group collection.

---

## Requirements — Attended Events

> **Semantics note:** a user "attends" an event by virtue of being an attendee of
> the group that hosts it. The collection therefore surfaces events belonging to
> groups the user is an attendee of, annotated with when that attendance began.

### EA-1 — Retrieve a profile's attended events
A client can request the collection of events a given user attended, identified by
that user's **username**.

**Acceptance**
- Given an existing username attending one or more events, the response contains
  those events.
- An event the user has no attendance relationship with never appears.

### EA-2 — Event entry contents
Each returned event **MUST** include:
- the event's identity: **name**, **slug**, **description**, and **images**;
- the **host group**, carrying its **id** and **name**;
- the **cities the event was hosted in**, each carrying **id**, **name**,
  **latitude**, and **longitude**;
- the **date/time the user's attendance began**.

**Acceptance**
- An event hosted in multiple cities returns all of them.
- An event with no hosted cities returns an **empty** city list, never null/absent.

### EA-3 — Ordering
Attended events **MUST** be ordered by the user's attendance time (earliest first),
and within the same attendance time by **event name, descending**.

**Acceptance**
- Given events across two attendance times, the earlier-attended group's events sort first.
- Events sharing an attendance time appear in reverse-alphabetical name order.

### EA-4 — Pagination
The collection **MUST** use the platform's standard pagination (same guarantees as GJ-4).

**Acceptance**
- A page of size *N* returns at most *N* events.
- Consecutive pages contain no duplicated and no skipped events.

### EA-5 — Empty collection
A user who exists but attended no events — or whose attended events don't match an
active search term — **MUST** yield an empty, well-formed page rather than an error.

**Acceptance**
- A user with zero attended events returns an empty, well-formed page.
- A search term matching none of the user's attended events returns an empty, well-formed page.

> **Unknown profile is an error, not an empty page** (same precondition as GJ-5):
> if the username does not exist, the profile lookup raises rather than returning
> an empty page.

### EA-6 — Filter by event name
The collection **MAY** be narrowed by an optional search term that matches the
**event name** case-insensitively, as a substring. When no search term is supplied,
all of the user's attended events are returned.

**Acceptance**
- Given a search term, only events whose name contains it (case-insensitive) appear.
- Pagination totals reflect the **filtered** set, not the full set.
- Omitting the search term returns the user's full attended-event collection.

---

## Out of scope (deferred to later phases)

The following are intentionally **not** in this spec; they are decided in
Technical Planning and Task Breakdown:
- the route shape and where the endpoints hang off the profile/group/event surface;
- repository method signatures and the read-model/entity classes that back the views;
- the request/response DTO split in the domain layer;
- the concrete pagination mechanism and parameters.

---

## Data Contract (canonical source of truth)

These views define the exact record shape each collection returns. Technical
Planning derives the read-model entities and DTOs from them; the spec requirements
above describe their behavior.

### `vw_group_attendees`

```sql
drop view if exists "alsaqr-2026".vw_group_attendees;

create view "alsaqr-2026".vw_group_attendees as
with detailed_attendees AS (
        select
                u.username as username,
                u.id as id,
                ga.group_id as group_id,
                ga.created_at as created_at
        from "alsaqr-2026".group_attendees ga
        inner join "alsaqr-2026".attendees a ON a.id = ga.attendee_id
        inner join "alsaqr-2026".users u ON u.id = a.user_id
),
detailed_groups AS (
        select
                g.id as group_id,
                g.name as group_name,
                g.description as group_description,
                g.images as group_images,
                g.slug as group_slug,
                c.id as hq_city_id,
                c.name as hq_city,
                c.country as hq_country,
                c.latitude as hq_latitude,
                c.longitude as hq_longitude,
                COALESCE(
                (
                        select
                                json_agg(json_build_object('id', t.id, 'name', t.name)) as json_agg
                        from
                                "alsaqr-2026".group_topics gt
                                join "alsaqr-2026".topics t on gt.topic_id = t.id
                        where
                                gt.group_id = g.id
                ),
                '[]'::json
                ) as topics
        from "alsaqr-2026".groups g
        inner join "alsaqr-2026".cities c on g.hq_city_id = c.id
)
select
        da.id,
        da.username,
        da.created_at as joined_at,
        dg.group_id,
        dg.group_name,
        dg.group_description,
        dg.group_images,
        dg.group_slug,
        dg.hq_city_id,
        dg.hq_city,
        dg.hq_country,
        dg.hq_latitude,
        dg.hq_longitude,
        dg.topics
from detailed_attendees da
inner join detailed_groups dg ON da.group_id = dg.group_id
order by da.created_at desc;
```

### `vw_event_attendees`

```sql
drop view if exists "alsaqr-2026".vw_event_attendees;

create view "alsaqr-2026".vw_event_attendees as
with detailed_attendees AS (
        select
                u.id as id,
                u.username as username,
                ga.group_id as group_id,
                ga.created_at as created_at
        from "alsaqr-2026".group_attendees ga
        inner join "alsaqr-2026".attendees a ON a.id = ga.attendee_id
        inner join "alsaqr-2026".users u ON u.id = a.user_id
),
detailed_events AS (
        select
                e.id AS event_id,
                e.slug AS event_slug,
                e.name AS event_name,
                e.description AS event_description,
                e.images AS event_images,
                g.id as group_id,
                g.name as group_name,
                COALESCE(
                        json_agg(
                                json_build_object(
                                        'id', c.id,
                                        'name', c.name,
                                        'latitude', c.latitude,
                                        'longitude', c.longitude
                                )
                        ) filter (
                        where c.id is not null
                        ),
                        '[]'::json
                ) as cities_hosted
        from "alsaqr-2026".events e
        join "alsaqr-2026".groups g on e.group_id = g.id
        left join "alsaqr-2026".event_cities ec on ec.event_id = e.id
        left join "alsaqr-2026".cities c on ec.city_id = c.id
        group by e.id, g.id
)
select
        de.event_id,
        de.event_slug,
        de.event_name,
        de.event_description,
        de.event_images,
        de.group_id,
        de.group_name,
        de.cities_hosted,
        da.username as username,
        da.created_at as joined_at
from detailed_events de
left join detailed_attendees da ON da.group_id = de.group_id
order by da.created_at, de.event_name DESC;
```

---

## Traceability stub (for Phase 2)

When `plan.md` is written, every planning item must trace back to a requirement ID
above (GJ-1…GJ-5, EA-1…EA-5). Suggested mapping seeds:

| Requirement | Planning concern |
|---|---|
| GJ-1, EA-1 | resolve profile by username (precondition), then username-keyed read endpoints |
| GJ-2, EA-2 | read-model entity (`Vw…` class) + output DTO derived from the data contract |
| GJ-3, EA-3 | ordering preserved from the view / query layer |
| GJ-4, EA-4 | standard pagination utility + filtered count source (e.g. count RPC) |
| GJ-5, EA-5 | empty-result handling on the filtered/empty case; unknown profile is an error |
| GJ-6, EA-6 | optional name search applied to query **and** count so totals stay consistent |