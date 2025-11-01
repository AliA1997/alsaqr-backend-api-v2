CREATE OR REPLACE FUNCTION get_my_events_total(
    userid varchar,
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar DEFAULT NULL,
    max_distance_km double precision DEFAULT NULL
)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    total_count bigint := 0;
BEGIN
    WITH user_groups AS (
        SELECT DISTINCT ga.group_id
        FROM group_attendees ga
        JOIN attendees a ON a.id = ga.attendee_id
        WHERE a.neo4j_user_id = userid
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
    SELECT COUNT(*)
    INTO total_count
    FROM events_with_distance ewd
    WHERE
        max_distance_km IS NULL
        OR ewd.distance_km <= max_distance_km;

    RETURN COALESCE(total_count, 0);
END;
$$;
