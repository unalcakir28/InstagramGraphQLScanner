using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstagramAPI.Models
{
    public class User
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string Biography { get; set; }
        public string ProfilePicture { get; set; }
        public string ProfilePictureHD { get; set; }
        public int FollowerCount { get; set; }
        public int FollowCount { get; set; }
        public int PostCount { get; set; }
    }
}
