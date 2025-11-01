
CREATE OR REPLACE FUNCTION get_selling_products(
    skip bigint,
    itemsPerPage bigint,
    userid character varying
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
    longitude double precision
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
        p.longitude
    FROM 
        products p
    INNER JOIN product_categories pc on p.product_category_id = pc.id
    WHERE p.neo4j_user_id = userid
    OFFSET skip
    LIMIT itemsPerPage;
END;
$$;
