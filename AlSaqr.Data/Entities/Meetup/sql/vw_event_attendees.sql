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
