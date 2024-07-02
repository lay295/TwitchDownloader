using System;
using System.Collections.Generic;

namespace TwitchDownloaderCore.TwitchObjects.Gql
{
    public class UserInfoData
    {
        public List<User> users { get; set; }
    }

    public class GqlUserInfoResponse
    {
        public UserInfoData data { get; set; }
        public Extensions extensions { get; set; }
    }

    public class User
    {
        public string id { get; set; }
        public string login { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
        public string description { get; set; }
        public string profileImageURL { get; set; }
    }
}
