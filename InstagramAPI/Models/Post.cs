using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstagramAPI.Models
{
    public class Post
    {
        public string Id { get; set; }
        public string Picture { get; set; }
        public string Thumbnail { get; set; }
        public string Caption { get; set; }
        public bool isVideo { get; set; }
        public string ShortCode { get; set; }
        public int CommentCount { get; set; }
        public int LikeCount { get; set; }
        public DateTime Date { get; set; }
        public string PostUrl { get; set; }
    }
}
