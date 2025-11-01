
CREATE OR REPLACE FUNCTION get_nearby_local_guides (
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar DEFAULT NULL,
    max_distance_km double precision DEFAULT NULL
)
RETURNS TABLE(
    id bigint,
    userid character varying,
    name character varying,
    city character varying,
    state_or_province character varying,
    country character varying,
    latitude double precision,
    longitude double precision,
    registered_at timestamptz,
    distance_km double precision
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        vlg.id,
        vlg.userid,
        vlg.name,
        vlg.city,
        vlg.state_or_province,
        vlg.country,
        vlg.latitude,
        vlg.longitude,
        vlg.registered_at,
        calculate_distance(vlg.latitude, vlg.longitude, target_lat, target_lon) AS distance_km
    FROM 
        vw_local_guides vlg
    WHERE 
        (
            search_term IS NULL
            OR vlg.name ILIKE '%' || search_term || '%'
        )
        AND ( max_distance_km IS NULL
          OR calculate_distance(vlg.latitude, vlg.longitude, target_lat, target_lon) <= max_distance_km )
    ORDER BY distance_km ASC
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;