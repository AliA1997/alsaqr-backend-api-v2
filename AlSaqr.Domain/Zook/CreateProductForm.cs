

namespace AlSaqr.Domain.Zook
{
    public class UpsertProductForm
    {

        public string? Title { get; set; }
        public string? Description { get; set; }
        public double? Price { get; set; }
        public IDictionary<string, object>? Attributes { get; set; }

        public Guid? ProductCategoryId { get; set; }
        public string[]? Images { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string? Country { get; set; }
        public string[]? Tags { get; set; }

    }

}
