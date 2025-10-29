CREATE OR REPLACE FUNCTION get_nearby_products_total(
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
    SELECT COUNT(*)
    INTO total_count
    FROM products p
    INNER JOIN product_categories pc ON p.product_category_id = pc.id
    WHERE 
        (max_distance_km IS NULL
         OR calculate_distance(p.latitude, p.longitude, target_lat, target_lon) <= max_distance_km)
        AND (search_term IS NULL OR p.title ILIKE '%' || search_term || '%');

    -- Ensure a value is always returned
    RETURN COALESCE(total_count, 0);
END;
$$;