
namespace AlSaqr.Domain.Utils
{
    public static class Location
    {
        public static class GeoUtils
        {
            public static double CalculateDistance(string latLong, double lat2, double lon2)
            {
                const double R = 6371; // Earth's radius in kilometers

                string[]? latLongs = null;
                if(!string.IsNullOrEmpty(latLong))
                    latLongs = latLong.Split(",");
                var lat1 = latLongs != null ? latLong[0] : 0;
                var lon1 = latLongs != null ? latLong[1] : 0;
                var dLat = ToRadians(lat2 - lat1);
                var dLon = ToRadians(lon2 - lon1);

                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

                return R * c;
            }

            private static double ToRadians(double angle) => angle * (Math.PI / 180);
        }

    }
}
