create or replace view vw_local_guides
as
select
  lg.id,
  lg.neo4j_user_id as userid,
  lg.name as name,
  c.name as city,
  c.state_or_province as state_or_province,
  c.country as country,
  c.latitude as latitude,
  c.longitude as longitude,
  lg.created_at as registered_at
from local_guides lg
inner join local_guides_cities lgc on lg.id = lgc.local_guides_id
inner join cities c on lgc.city_id = c.id
order by lg.created_at;