

CREATE OR REPLACE FUNCTION get_nearby_online_events (
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar DEFAULT NULL,
    max_distance_km double precision DEFAULT NULL
)
RETURNS TABLE(
    id bigint,
    slug character varying,
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
        voe.id,
        voe.slug,
        voe.name,
        voe.description,
        voe.images,
        voe.group_id,
        voe.group_name,
        voe.cities_hosted,
        -- Calculate the minimum distance among hosted cities
        (
            SELECT MIN(calculate_distance(
                (city_elem->>'latitude')::double precision,
                (city_elem->>'longitude')::double precision,
                target_lat,
                target_lon
            ))
            FROM json_array_elements(voe.cities_hosted) AS city_elem
        ) AS distance_km
    FROM 
        vw_online_events voe
    WHERE 
        (
            search_term IS NULL
            OR voe.name ILIKE '%' || search_term || '%'
            OR voe.description ILIKE '%' || search_term || '%'
        )
        AND (
            max_distance_km IS NULL
            OR EXISTS (
                SELECT 1
                FROM json_array_elements(voe.cities_hosted) AS city_elem
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
