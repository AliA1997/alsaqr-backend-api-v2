using AlSaqr.Data.Entities.Meetup;
using Supabase.Interfaces;
using Supabase.Postgrest.Interfaces;
using static AlSaqr.Domain.SocialMedia.User;
using static Supabase.Postgrest.Constants;

namespace AlSaqr.Data.Helpers
{
    public static class SupabaseHelper
    {

        /// <summary>
        /// True when <paramref name="userId"/> is the founder (organizer) of the group.
        /// The founder is the group_attendees row flagged is_group_organizer, whose
        /// attendee_id resolves back to the user via the attendees table. Shared by the
        /// group and event repositories so the rule lives in one place.
        /// </summary>
        public static async Task<bool> IsGroupFounder(
            Supabase.Client client,
            Guid groupId,
            Guid userId,
            CancellationToken ct = default)
        {
            var attendee = (await client.From<Attendee>()
                .Filter("user_id", Operator.Equals, userId.ToString())
                .Get(ct)).Models.FirstOrDefault();

            if (attendee == null)
                return false;

            var organizerLink = (await client.From<GroupAttendees>()
                .Filter("group_id", Operator.Equals, groupId.ToString())
                .Filter("attendee_id", Operator.Equals, attendee.Id.ToString())
                .Filter("is_group_organizer", Operator.Equals, "true")
                .Get(ct)).Models.FirstOrDefault();

            return organizerLink != null;
        }


         public static IDictionary<string, object> DefineGetSimilarGroupsParams(
            Guid groupId,
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
            Guid localGuideId,
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
        public static IDictionary<string, object> DefinePagingGetMyEventsOrGroupsParams(
             string userId,
             string latitude,
             string longitude,
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
            Guid productId,
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
            Guid productCategoryId,
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
                    { "max_distance_km", maxDistanceKm }
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
                    { "max_distance_km", maxDistanceKm }
                };
            }
        }

        public static IDictionary<string, dynamic> DefineGetUsersToAddParams(
            Guid currentUserId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            return new Dictionary<string, dynamic>
            {
                { "current_user_id", currentUserId },
                { "search_term", searchTerm },
                { "current_page", currentPage },
                { "items_per_page", itemsPerPage }
            };
        }

        public static IDictionary<string, dynamic> DefineGetPostsToAddParams(
            Guid currentUserId,
            string? searchTerm,
            int currentPage,
            int itemsPerPage)
        {
            return new Dictionary<string, dynamic>
            {
                { "current_user_id", currentUserId },
                { "search_term", searchTerm },
                { "current_page", currentPage },
                { "items_per_page", itemsPerPage }
            };
        }

        public static IDictionary<string, dynamic> DefineGetMessageParams(
            Guid currentUserId,
            string? searchTerm)
        {
            return new Dictionary<string, dynamic>
            {
                { "p_user_id", currentUserId },
                { "p_search_term", searchTerm }
            };
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
