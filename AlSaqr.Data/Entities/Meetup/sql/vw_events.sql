CREATE VIEW vw_events AS
SELECT
  e.id,
  e.name,
  e.description,
  e.images,
  g.id AS group_id,
  g.name AS group_name,
  COALESCE(
    JSON_AGG(
      JSON_BUILD_OBJECT(
        'id', c.id,
        'name', c.name,
        'latitude', c.latitude,
        'longitude', c.longitude
      )
    ) FILTER (WHERE c.id IS NOT NULL),
    '[]'
  ) AS cities_hosted
FROM events e 
INNER JOIN groups g ON e.group_id = g.id
LEFT JOIN event_cities ec ON ec.event_id = e.id
LEFT JOIN cities c ON ec.city_id = c.id
GROUP BY e.id, g.id
ORDER BY e.id;