namespace AlSaqr.Domain.Meetup
{
    public class TopicDto
    {
        public TopicDto(dynamic details)
        {
            this.Id = details.Id;
            this.Name = details.Name;
        }
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

    }
}
