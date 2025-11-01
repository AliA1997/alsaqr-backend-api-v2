CREATE OR REPLACE FUNCTION get_nearby_local_guides_for_guide_total(
    localguideid bigint,
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
    WITH guide_distances AS (
        SELECT 
            vlg.id,
            (
                SELECT MIN(
                    calculate_distance(
                        (city_elem->>'latitude')::double precision,
                        (city_elem->>'longitude')::double precision,
                        target_lat,
                        target_lon
                    )
                )
                FROM json_array_elements(vlg.cities_hosted) AS city_elem
            ) AS distance_km
        FROM vw_local_guides vlg
        WHERE 
            vlg.id != localguideid
            AND (
                search_term IS NULL
                OR vlg.name ILIKE '%' || search_term || '%'
            )
    )
    SELECT COUNT(*)
    INTO total_count
    FROM guide_distances gd
    WHERE 
        max_distance_km IS NULL
        OR gd.distance_km <= max_distance_km;

    RETURN COALESCE(total_count, 0);
END;
$$;