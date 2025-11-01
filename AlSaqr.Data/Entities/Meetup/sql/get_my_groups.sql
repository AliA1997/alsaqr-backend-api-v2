CREATE OR REPLACE FUNCTION get_my_groups(
    userid varchar,
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar default null,
    max_distance_km double precision DEFAULT NULL
)
RETURNS TABLE(
    id bigint,
    name character varying,
    description text,
    images json,
    hq_city_id bigint,
    hq_city character varying,
    hq_country character varying,
    topics json,
    attendees json,
    latitude double precision,
    longitude double precision,
    distance_km double precision
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        vg.id,
        vg.name, 
        vg.description,
        vg.images,
        vg.hq_city_id,
        vg.hq_city,
        vg.hq_country,
        vg.topics,
        vg.attendees,
        vg.hq_latitude,
        vg.hq_longitude,
        calculate_distance(vg.hq_latitude, vg.hq_longitude, target_lat, target_lon) AS distance_km
    FROM 
        vw_groups vg
    INNER JOIN group_attendees ga ON ga.group_id = vg.id
    INNER JOIN attendees a ON a.id = ga.attendee_id
    WHERE 
        a.neo4j_user_id = userid
        AND
        ( max_distance_km IS NULL
          OR calculate_distance(vg.hq_latitude, vg.hq_longitude, target_lat, target_lon) <= max_distance_km )
    ORDER BY 
        distance_km ASC
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;