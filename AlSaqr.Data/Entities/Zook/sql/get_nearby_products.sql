
CREATE OR REPLACE FUNCTION get_nearby_products(
    skip bigint,
    itemsPerPage bigint,
    target_lat double precision,
    target_lon double precision,
    search_term varchar default null,
    max_distance_km double precision DEFAULT NULL
)
RETURNS TABLE(
    id bigint,
    title character varying,
    description text,
    price double precision,
    images json,
    slug character varying,
    attributes json,
    tags json,
    product_category_id bigint,
    category character varying,
    country character varying,
    user_id character varying,
    latitude double precision,
    longitude double precision,
    distance_km double precision
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        p.id,
        p.title,
        p.description,
        p.price,
        p.images,
        p.slug,
        p.attributes,
        p.tags,
        p.product_category_id,
        p.country,
        pc.name as category,
        p.neo4j_user_id as user_id,
        p.latitude,
        p.longitude,
        calculate_distance(p.latitude, p.longitude, target_lat, target_lon) AS distance_km
    FROM 
        products p
    INNER JOIN product_categories pc on p.product_category_id = pc.id
    WHERE 
        ( max_distance_km IS NULL
          OR calculate_distance(p.latitude, p.longitude, target_lat, target_lon) <= max_distance_km )
    ORDER BY 
        distance_km ASC
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;
