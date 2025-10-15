namespace  AlSaqr.Domain.Utils
{
    public static class List
    {
        public class CreateListFormDto : CreateListForm
        {
            public string[] UsersAdded { get; set; }
            public string[] PostsAdded { get; set; }
        }
        public class CreateListForm
        {
            public string Name { get; set; }
            public string AvatarOrBannerImage { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
            public object[] UsersAdded { get; set; }
            public object[] PostsAdded { get; set; }
        }

        public class SaveItemToListDto
        {
            public string RelatedEntityId { get; set; }
            public string Type { get; set; }
        }

    }
}
