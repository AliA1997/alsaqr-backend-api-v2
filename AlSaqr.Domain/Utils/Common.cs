using System.Text.Json.Serialization;

namespace  AlSaqr.Domain.Utils
{
    public static class Common
    {

        public class Pagination
        {
            public int ItemsPerPage { get; set; }
            public int CurrentPage { get; set; }
            public int TotalItems { get; set; }
            public int TotalPages { get; set; }
        }

        public class PaginatedResult<T>
        {
            public IEnumerable<T> Items { get; }
            public Pagination Pagination { get; }

            public PaginatedResult(IEnumerable<T> items, Pagination pagination)
            {
                Items = items;
                Pagination = pagination;
            }
        }

        public class Neo4jPaginatedObj
        {
            [JsonPropertyName("skip")]
            public string? Skip { get; set; }
            [JsonPropertyName("itemsPerPage")]
            public string? ItemsPerPage { get; set; }

            [JsonPropertyName("searchTerm")]
            public string? SearchTerm { get; set; }
        }

        public class AlSaqrPostRequest<T>
        {
            [JsonPropertyName("values")]
            public T Values { get; set; }
        }
    }
}
