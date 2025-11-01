
CREATE OR REPLACE FUNCTION get_similar_groups(
    group_id bigint,
    target_lat double precision,
    target_lon double precision
)
RETURNS TABLE(
    id bigint,
    slug character varying,
    name character varying,
    description text,
    images json,
    hq_city_id bigint,
    hq_city character varying,
    hq_country character varying,
    hq_latitude double precision,
    hq_longitude double precision,
    distance_km double precision,
    description_similarity double precision
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        g.id,
        g.slug,
        g.name,
        g.description,
        g.images,
        g.hq_city_id,
        g.hq_city,
        g.hq_country,
        g.hq_latitude,
        g.hq_longitude,
        calculate_distance(g.hq_latitude, g.hq_longitude, target_lat, target_lon) AS distance_km,
        similarity(g.description::text, q.description::text)::double precision AS description_similarity
    FROM vw_groups g
    JOIN vw_groups q ON q.id = group_id
    WHERE g.id != q.id
    ORDER BY description_similarity DESC, distance_km ASC
    LIMIT 10;
END;
$$;
