
CREATE OR REPLACE FUNCTION get_nearby_events (
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
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        ve.id,
        ve.name,
        ve.description,
        ve.images,
        ve.group_id,
        ve.group_name,
        ve.cities_hosted,
        -- Calculate the minimum distance among hosted cities
        (
            SELECT MIN(calculate_distance(
                (city_elem->>'latitude')::double precision,
                (city_elem->>'longitude')::double precision,
                target_lat,
                target_lon
            ))
            FROM json_array_elements(ve.cities_hosted) AS city_elem
        ) AS distance_km
    FROM 
        vw_events ve
    WHERE 
        (
            search_term IS NULL
            OR ve.name ILIKE '%' || search_term || '%'
            OR ve.description ILIKE '%' || search_term || '%'
        )
        AND (
            max_distance_km IS NULL
            OR EXISTS (
                SELECT 1
                FROM json_array_elements(ve.cities_hosted) AS city_elem
                WHERE calculate_distance(
                    (city_elem->>'latitude')::double precision,
                    (city_elem->>'longitude')::double precision,
                    target_lat,
                    target_lon
                ) <= max_distance_km
            )
        )
    ORDER BY distance_km ASC
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;
