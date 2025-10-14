namespace AlSaqr.API.Utils
{
    public static class CommunityDiscussion
    {
        public class CommunityDiscussionInviteConfirmationDto
        {
            public string Username { get; set; }
            public string Email { get; set; }
        }

        public class AcceptOrDenyCommunityDiscussionInviteConfirmationDto
        {
            public bool Accept { get; set; }
            public bool Deny { get; set; }
        }

        public class CommunityDiscussionMessageDto
        {
            public string UserId { get; set; }
            public string CommunityId { get; set; }
            public string CommunityDiscussionId { get; set; }
            public string MessageText { get; set; }
            public string Image { get; set; }
            public string _Type { get; set; } = "community_discussion_message";
            public string[] Tags { get; set; }
        }
    }
}
