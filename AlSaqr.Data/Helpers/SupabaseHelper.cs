using Supabase.Interfaces;
using Supabase.Postgrest.Interfaces;
using static AlSaqr.Domain.Utils.User;

namespace AlSaqr.Data.Helpers
{
    public static class SupabaseHelper
    {

         public static IDictionary<string, object> DefineGetSimilarGroupsParams(
            int groupId,
            string latitude,
            string longitude
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

         
            return new Dictionary<string, object>()
            {
                { "group_id", groupId },
                { "target_lat", latitudeParsed },
                { "target_lon", longitudeParsed },
           };
        }

        public static IDictionary<string, object> DefineGetLocalGuideForCurrentLocalGuideParams(
            int localGuideId,
            string latitude,
            string longitude,
            int skip,
            int currentPage,
            int itemsPerPage,
            double? maxDistanceKm = null,
            string? searchTerm = null
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "localguideid", localGuideId },
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "localguideid", localGuideId },
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }

        public static IDictionary<string, object> DefineGetLocalGuideParams(
            string latitude,
            string longitude,
            int skip,
            int currentPage,
            int itemsPerPage,
            double? maxDistanceKm = null,
            string? searchTerm = null
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }

        public static IDictionary<string, object> DefineGetMyEventsOrGroupsParams(
             string userId,
             string latitude,
             string longitude,
             int skip,
             int currentPage,
             int itemsPerPage,
             double? maxDistanceKm = null,
             string? searchTerm = null
         )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "userid", userId },
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "userid", userId },
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }
        public static IDictionary<string, object> DefineGetEventsParams(
            string latitude,
            string longitude,
            int skip,
            int currentPage,
            int itemsPerPage,
            double? maxDistanceKm = null,
            string? searchTerm = null
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }

        public static IDictionary<string, object> DefineGetGroupsParams(
            string latitude,
            string longitude,
            int skip,
            int currentPage,
            int itemsPerPage,
            double? maxDistanceKm = null,
            string? searchTerm = null
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }

        public static IDictionary<string, object> DefineGetSellingProductsParams(
            string userId,
            int skip,
            int itemsPerPage)
        {
            return new Dictionary<string, object>()
            {
                { "skip", skip },
                { "itemsperpage", itemsPerPage },
                { "userid", userId }
            };
        }

        public static IDictionary<string, object> DefineGetProductDetailsParams(
            int productId,
            string latitude,
            string longitude)
        {
           var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);
            return new Dictionary<string, object>()
            {
                { "product_id", productId },
                { "target_lat", latitudeParsed },
                { "target_lon", longitudeParsed }
            };
        }

        public static IDictionary<string, object> DefineGetProductParams(
                string latitude,
                string longitude,
                int skip,
                int currentPage,
                int itemsPerPage,
                double? maxDistanceKm = null,
                string? searchTerm = null
            )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if(!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", searchTerm },
                    { "max_distance_km", maxDistanceKm },
                };
            } 
            else
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "search_term", null },
                    { "max_distance_km", maxDistanceKm },
                };
            }
        }

        public static IDictionary<string, object> DefineGetProductByCategoryParams(
            string latitude,
            string longitude,
            int productCategoryId,
            int skip,
            int currentPage,
            int itemsPerPage,
            int? maxDistanceKm = null,
            string? searchTerm = null
        )
        {
            var latitudeParsed = double.Parse(latitude);
            var longitudeParsed = double.Parse(longitude);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "target_category_id", productCategoryId },
                    { "max_distance_km", maxDistanceKm },
                    { "search_term", searchTerm }
                };
            }
            else
            {
                return new Dictionary<string, object>()
                {
                    { "skip", skip },
                    { "itemsperpage", itemsPerPage },
                    { "target_lat", latitudeParsed },
                    { "target_lon", longitudeParsed },
                    { "target_category_id", productCategoryId },
                    { "max_distance_km", maxDistanceKm },
                    { "search_term", maxDistanceKm }
                };
            }
        }

        public static async Task<string> CallFunction(
            Supabase.Client supabaseClient,
            string functionName,
            IDictionary<string,object> functionParams)
        {
            var results = await supabaseClient.Rpc(functionName, functionParams);
            return results.Content?.ToString() ?? "";
        }
    }
}
