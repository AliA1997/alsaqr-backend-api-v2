namespace  AlSaqr.Domain.Utils
{
    public static class Posts
    {
        public class BookmarkRequest
        {
            public string StatusId { get; set; }
            public string UserId { get; set; }
            public bool Bookmarked { get; set; }
        }

        public class LikePostRequest
        {
            public string StatusId { get; set; }
            public string UserId { get; set; }
            public bool Liked { get; set; }
        }

        public class RePostRequest
        {
            public string StatusId { get; set; }
            public string UserId { get; set; }
            public bool Reposted { get; set; }
        }
    }
}
