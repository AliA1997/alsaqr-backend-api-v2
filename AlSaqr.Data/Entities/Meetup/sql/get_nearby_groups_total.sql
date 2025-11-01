CREATE OR REPLACE FUNCTION get_nearby_groups_total(
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
        vw_groups vg
    WHERE 
        ( max_distance_km IS NULL
          OR calculate_distance(vg.hq_latitude, vg.hq_longitude, target_lat, target_lon) <= max_distance_km );
    -- Ensure a value is always returned
    RETURN COALESCE(total_count, 0);
END;
$$;