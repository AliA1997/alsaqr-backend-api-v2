
CREATE OR REPLACE FUNCTION get_nearby_local_guides_for_guide (
    localguideid bigint,
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
    userid character varying,
    name character varying,
    cities_hosted json,
    registered_at timestamptz,
    distance_km double precision
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH guide_distances AS (
        SELECT 
            vlg.id,
            vlg.slug,
            vlg.userid,
            vlg.name,
            vlg.cities_hosted,
            vlg.registered_at,
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
    SELECT 
        gd.id,
        gd.slug,
        gd.userid,
        gd.name,
        gd.cities_hosted,
        gd.registered_at,
        gd.distance_km
    FROM guide_distances gd
    WHERE 
        max_distance_km IS NULL
        OR gd.distance_km <= max_distance_km
    ORDER BY gd.distance_km ASC
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;
