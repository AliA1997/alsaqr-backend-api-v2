create or replace view vw_local_guides
as
select
  lg.id,
  lg.slug,
  lg.neo4j_user_id as userid,
  lg.name as name,
  COALESCE(
    JSON_AGG(
      JSON_BUILD_OBJECT(
        'id', c.id,
        'name', c.name,
        'stateOrProvince', c.state_or_province,
        'country', c.country,
        'latitude', c.latitude,
        'longitude', c.longitude
      )
    ) FILTER (WHERE c.id IS NOT NULL),
    '[]'
  ) AS cities_hosted,
  lg.created_at as registered_at
from local_guides lg
LEFT join local_guides_cities lgc on lg.id = lgc.local_guides_id
LEFT join cities c on lgc.city_id = c.id
group by lg.neo4j_user_id, lg.id, lg.slug, lg.name, lg.created_at
order by lg.id;
