CREATE OR REPLACE FUNCTION get_nearby_local_guides_total(
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
          vw_local_guides vlg
    WHERE 
        (
            search_term IS NULL
            OR vlg.name ILIKE '%' || search_term || '%'
        )
        AND ( max_distance_km IS NULL
        OR calculate_distance(vlg.latitude, vlg.longitude, target_lat, target_lon) <= max_distance_km );
    -- Ensure a value is always returned
    RETURN COALESCE(total_count, 0);
END;
$$;