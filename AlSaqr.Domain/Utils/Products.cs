using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.Utils
{
    public static class Products
    {
        public class CreateProductForm
        {

            public string? Title { get; set; }
            public string? Description { get; set; }
            public double? Price { get; set; }
            public IDictionary<string, object>? Attributes { get; set; }
            
            public int? ProductCategoryId { get; set; }
            public string[]? Images { get; set; }
            
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }

            public string? Country { get; set; }
            public string[]? Tags { get; set; }

        }

        public class UpsertProductForm: CreateProductForm
        {
            public string[] FieldsToUpdate { get; set; }
        }
    }
}
