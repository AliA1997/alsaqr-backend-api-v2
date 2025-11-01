CREATE OR REPLACE FUNCTION get_nearby_events_total(
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar default null,
    max_distance_km double precision DEFAULT NULL
)
RETURNS bigint
LANGUAGE plpgsql
AS $$
DECLARE
    total_count bigint := 0;
BEGIN
    SELECT COUNT(*)
    INTO total_count
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
            );
    -- Ensure a value is always returned
    RETURN COALESCE(total_count, 0);
END;
$$;