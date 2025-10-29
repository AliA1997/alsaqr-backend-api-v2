-- Create a function to calculate distance between two points
CREATE OR REPLACE FUNCTION calculate_distance(
    lat1 double precision,
    lon1 double precision,
    lat2 double precision,
    lon2 double precision
)
RETURNS double precision
LANGUAGE plpgsql
AS $$
DECLARE
    R constant double precision := 6371; -- Earth's radius in kilometers
    dlat double precision;
    dlon double precision;
    a double precision;
    c double precision;
BEGIN
    -- Convert degrees to radians
    dlat := radians(lat2 - lat1);
    dlon := radians(lon2 - lon1);
    
    -- Haversine formula
    a := sin(dlat/2) * sin(dlat/2) + 
         cos(radians(lat1)) * cos(radians(lat2)) * 
         sin(dlon/2) * sin(dlon/2);
    
    c := 2 * atan2(sqrt(a), sqrt(1-a));
    
    RETURN R * c;
END;
$$;