namespace  AlSaqr.Domain.Utils
{
    public static class Posts
    {
        public class CreatePostDto
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public string? Image { get; set; }
            public string CreatedAt { get; set; }
            public string UpdatedAt { get; set; }
            public string _Rev { get; set; }
            public string _Type { get; set; } = "post";
            public bool BlockTweet { get; set; }
            public string[] Tags { get; set; }
            public string[]? Likes { get; set; }
            public string? UserId { get; set; }
        }

        public class CreateCommentDto
        {
            public string Id { get; set; }
            public string Text { get; set; }
            public string? CommentToCommentOnId { get; set; }
            public string PostId { get; set; }
            public string UserId { get; set; }
            public string? Image { get; set; }
            public string? CreatedAt { get; set; }
            public string? UpdatedAt { get; set; }
        }
    }
}
