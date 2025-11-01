
CREATE OR REPLACE FUNCTION get_my_events (
    userid varchar,
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar DEFAULT NULL,
    max_distance_km double precision DEFAULT NULL
)
RETURNS TABLE(
    id bigint,
    name character varying,
    description text,
    images json,
    group_id bigint,
    group_name character varying,
    cities_hosted json,
    distance_km double precision
)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH user_groups AS (
        SELECT DISTINCT ga.group_id
        FROM group_attendees ga
        JOIN attendees a ON a.id = ga.attendee_id
        WHERE a.neo4j_user_id = neo4j_user_id
    ),
    events_with_distance AS (
        SELECT 
            ve.id,
            ve.name,
            ve.description,
            ve.images,
            ve.group_id,
            ve.group_name,
            ve.cities_hosted,
            -- Compute min distance just once per event
            (
                SELECT MIN(calculate_distance(
                    (city_elem->>'latitude')::double precision,
                    (city_elem->>'longitude')::double precision,
                    target_lat,
                    target_lon
                ))
                FROM json_array_elements(ve.cities_hosted) AS city_elem
            ) AS distance_km
        FROM vw_events ve
        JOIN user_groups ug ON ug.group_id = ve.group_id
        WHERE 
            search_term IS NULL
            OR ve.name ILIKE '%' || search_term || '%'
            OR ve.description ILIKE '%' || search_term || '%'
    )
    SELECT 
        ewd.id,
        ewd.name,
        ewd.description,
        ewd.images,
        ewd.group_id,
        ewd.group_name,
        ewd.cities_hosted,
        ewd.distance_km
    FROM events_with_distance ewd
    WHERE
        max_distance_km IS NULL
        OR ewd.distance_km <= max_distance_km
    ORDER BY ewd.distance_km ASC NULLS LAST
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;