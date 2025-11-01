create view vw_online_events as
select
  e.id,
  e.name,
  e.description,
  e.images,
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
    ) FILTER (WHERE c.id IS NOT NULL),
    '[]'
  ) AS cities_hosted
from events e 
inner join groups g ON e.group_id = g.id
LEFT JOIN event_cities ec ON ec.event_id = e.id
LEFT JOIN cities c ON ec.city_id = c.id
where e.is_online = true
GROUP BY e.id, e.name, e.description, g.id, g.name
ORDER BY e.id;