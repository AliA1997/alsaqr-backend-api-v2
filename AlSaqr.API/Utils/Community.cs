namespace AlSaqr.API.Utils
{
    public static class Community
    {
        public class CreateCommunityFormDto: CreateCommunityForm
        {
            public string[] UsersAdded { get; set; }
            public string[] PostsAdded { get; set; }
        }
        public class CreateCommunityForm 
        { 
            public string Name { get; set; }
            public string AvatarOrBannerImage { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
            public object[] UsersAdded { get; set; }
            public object[] PostsAdded { get; set; }
        }

        public class UpdateCommunityForm
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Avatar { get; set; }
            public string IsPrivate { get; set; }
            public string[] Tags { get; set; }
        }

        public class CommunityInviteConfirmationDto 
        { 
            public string Username { get; set; }
            public string Email { get; set; }
        }

        public class AcceptOrDenyCommunityInviteConfirmationDto
        {
            public bool Accept { get; set; }
            public bool Deny { get; set; }
        }

    }
}
