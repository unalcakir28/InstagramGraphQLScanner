using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstagramAPI.Models
{
    public class Comment
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public DateTime Date { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPicture { get; set; }
    }
}
