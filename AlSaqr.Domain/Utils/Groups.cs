using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlSaqr.Domain.Utils
{
    public class Groups
    {

        public class CreateGroupForm
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public IDictionary<string, object>[]? Images { get; set; }
            public int? HqCityId { get; set; }
            public string? HqCity { get; set; }
            public string? HqStateOrProvince { get; set; }
            public string? HqCountry { get; set; }
            public decimal? HqLatitude { get; set; }
            public decimal? HqLongitude { get; set; }
            public IDictionary<string, object>[] Topics { get; set; }
            public IDictionary<string, object>[] Attendees { get; set; }
        }

        public class UpsertGroupForm : CreateGroupForm
        {
            public string[] FieldsToUpdate { get; set; }
        }
    }
}
