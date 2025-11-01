CREATE VIEW vw_groups AS
SELECT
  g.id,
  g.name,
  g.images,
  c.id AS hq_city_id,
  c.name AS hq_city,
  c.country as hq_country,
  c.latitude as hq_latitude,
  c.longitude as hq_longitude,
  g.description,
  COALESCE(
    (
      SELECT json_agg(
        json_build_object(
          'id', t.id,
          'name', t.name
        )
      )
      FROM group_topics gt
      JOIN topics t ON gt.topic_id = t.id
      WHERE gt.group_id = g.id
    ),
    '[]'
  ) AS topics,
  COALESCE(
    (
      SELECT json_agg(
        json_build_object(
          'id', a.id,
          'name', a.name
        )
      )
      FROM group_attendees ga
      JOIN attendees a ON ga.attendee_id = a.id
      WHERE ga.group_id = g.id
    ),
    '[]'
  ) AS attendees
FROM public.groups g
INNER JOIN public.cities c ON g.hq_city_id = c.id
ORDER BY g.id;