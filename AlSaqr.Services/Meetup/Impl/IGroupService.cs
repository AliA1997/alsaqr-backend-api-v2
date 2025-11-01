using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AlSaqr.Domain.Utils.Groups;

namespace AlSaqr.Services.Meetup.Impl
{
    public interface IGroupService
    {
        Task InsertGroup(CreateGroupForm form);
    }
}
