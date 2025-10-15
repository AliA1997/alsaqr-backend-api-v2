namespace  AlSaqr.Domain.Utils
{
    public static class Messages
    {
        public class MessageFormDto
        {
            public string MessageType { get; set; }
            public string SenderId { get; set; }
            public string SenderProfileImg { get; set; }
            public string SenderUsername { get; set; }
            public string RecipientId { get; set; }
            public string RecipientProfileImg { get; set; }
            public string RecipientUsername { get; set; }

            public string Text { get; set; }
            public string Image { get; set; }
        }
    }
}
