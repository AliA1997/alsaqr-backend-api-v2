CREATE OR REPLACE FUNCTION get_nearby_products_by_category_total(
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    target_category_id bigint,
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
        p.product_category_id = target_category_id
        AND ( max_distance_km IS NULL
            OR calculate_distance(p.latitude, p.longitude, target_lat, target_lon) <= max_distance_km );

    -- Ensure a value is always returned
    RETURN COALESCE(total_count, 0);
END;
$$;