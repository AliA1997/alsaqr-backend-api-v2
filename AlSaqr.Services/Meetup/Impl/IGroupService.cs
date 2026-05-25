using AlSaqr.Domain.Meetup;

namespace AlSaqr.Services.Meetup.Impl
{
    public interface IGroupService
    {
        Task InsertGroup(CreateGroupForm form);
    }
}
