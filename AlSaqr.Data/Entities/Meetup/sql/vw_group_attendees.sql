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
