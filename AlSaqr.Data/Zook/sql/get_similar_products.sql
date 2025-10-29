CREATE OR REPLACE FUNCTION get_similar_products(
    product_id bigint,
    target_lat double precision,
    target_lon double precision
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
    distance_km double precision,
    title_similarity double precision,
    description_similarity double precision,
    category_similarity double precision
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
        p.category as category,
        p.neo4j_user_id as user_id,
        p.latitude,
        p.longitude,
        calculate_distance(p.latitude, p.longitude, target_lat, target_lon) AS distance_km,
        similarity(p.title::text, q.title::text)::double precision AS title_similarity,
        similarity(p.description::text, q.description::text)::double precision AS description_similarity,       
        similarity(p.category::text, q.category::text)::double precision AS category_similarity
    FROM (
        Select 
            p1.*, 
            c1.name as category
        from products p1
        inner join product_categories c1 on c1.id = p1.product_category_id
    ) p
    JOIN (
        Select 
            p2.*, 
            c2.name  as category
        from products p2
        inner join product_categories c2 on c2.id = p2.product_category_id
    ) q ON q.id = product_id
    WHERE p.id != q.id
    ORDER BY (similarity(p.title::text, q.title::text) + similarity(p.description::text, q.description::text) + similarity(p.category::text, q.category::text)) DESC
    LIMIT 10;
    END;
$$;