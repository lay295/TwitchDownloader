namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class UserId
    {
        public string id { get; set; }
    }

    public class UserIdData
    {
        public UserId[] users { get; set; }
    }

    public class GqlUserIdResponse
    {
        public UserIdData data { get; set; }
        public Extensions extensions { get; set; }
    }
}