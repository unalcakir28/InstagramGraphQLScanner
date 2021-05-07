using InstagramAPI.Models;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramAPI
{
    public class Functions
    {
        Random rnd = new Random();

        /// <summary>
        /// Kullanıcı adı ile instagram veritabanındaki public kullanıcı verilerini getirir.
        /// </summary>
        /// <param name="userName">Zorunludur. Verileri getirilecek kullanıcının instagram kullanıcı adı girilmelidir. https://www.instagram.com/X/ url'indeki X alanında yazan değerdir</param>
        /// <param name="cookies">Zorunludur. Aktif cookie verileri tarayıcıdan alınarak girilebilir.</param>
        public User GetUser(string userName, Dictionary<string, string> cookies)
        {
            var client = new RestClient("https://www.instagram.com/" + userName + "/?__a=1");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);

            cookies.ToList().ForEach(cookie =>
            {
                request.AddCookie(cookie.Key, cookie.Value);
            });

            IRestResponse response = client.Execute(request);
            Thread.Sleep(rnd.Next(3000, 5000));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new Exception("Kullanıcı bulunamadı.");

            dynamic data = JObject.Parse(response.Content);

            var user = new User();
            user.Id = data.graphql.user.id;
            user.UserName = data.graphql.user.username;
            user.FullName = data.graphql.user.full_name;
            user.Biography = data.graphql.user.biography;
            user.ProfilePicture = data.graphql.user.profile_pic_url;
            user.ProfilePictureHD = data.graphql.user.profile_pic_url_hd;
            user.FollowerCount = data.graphql.user.edge_followed_by.count;
            user.FollowCount = data.graphql.user.edge_follow.count;
            user.PostCount = data.graphql.user.edge_owner_to_timeline_media.count;

            return user;
        }

        /// <summary>
        /// Hashtag altına yapılmış olan public paylaşımları getirir.
        /// </summary>
        /// <param name="tag">Zorunludur.</param>
        /// <param name="cookies">Zorunludur. Aktif cookie verileri tarayıcıdan alınarak girilebilir.</param>
        /// <param name="after">Zorunlu değildir. Sayfalama için kullanılır.</param>
        /// <param name="postPerPage">Zorunlu değildir. Default olarak 50 kullanılır ve maksimum 50 girilebilir. Her sayfada kaç post getirileceğini belirtir.</param>
        /// <param name="pageCount">Zorunlu değildir. Kaç sayfalık post getirileceğini belirtir.</param>
        public List<Post> GetPostsFromTag(string tag, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=9b498c08113f1e09617a1703c22b2f32&variables={\"tag_name\":\"" + tag + "\",\"first\":" + postPerPage.ToString() + ",\"after\":\"" + after + "\"}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);

            cookies.ToList().ForEach(cookie =>
            {
                request.AddCookie(cookie.Key, cookie.Value);
            });

            IRestResponse response = client.Execute(request);
            Thread.Sleep(rnd.Next(3000, 5000));

            dynamic data = JObject.Parse(response.Content);
            var edges = data.data.hashtag.edge_hashtag_to_media.edges;

            var posts = new List<Post>();
            foreach (var edge in edges)
            {
                var post = new Post();
                post.Id = edge.node.id;
                post.Picture = edge.node.display_url;
                post.Thumbnail = edge.node.thumbnail_src;
                post.Caption = (edge.node.edge_media_to_caption.edges as IList).Count > 0 ? edge.node.edge_media_to_caption.edges[0].node.text : "";
                post.isVideo = edge.node.is_video;
                post.ShortCode = edge.node.shortcode;
                post.CommentCount = edge.node.edge_media_to_comment.count;
                post.LikeCount = edge.node.edge_media_preview_like.count;
                post.Date = Tools.UnixTimeStampToDateTime((double)edge.node.taken_at_timestamp);
                post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                posts.Add(post);
            }

            bool has_next_page = data.data.hashtag.edge_hashtag_to_media.page_info.has_next_page;
            string end_cursor = data.data.hashtag.edge_hashtag_to_media.page_info.end_cursor;

            if (!has_next_page || pageCount == 1)
                return posts;

            var nextPosts = GetPostsFromTag(tag, cookies, postPerPage: postPerPage, pageCount: --pageCount, after: end_cursor);
            posts.AddRange(nextPosts);
            return posts;
        }

        /// <summary>
        /// Kullanıcının paylaştığı postları getirir.
        /// </summary>
        /// <param name="userId">Zorunludur. GetUser fonksiyonu yardımı ile userId bilgisi alınabilir.</param>
        /// <param name="cookies">Zorunludur. Aktif cookie verileri tarayıcıdan alınarak girilebilir.</param>
        /// <param name="after">Zorunlu değildir. Sayfalama için kullanılır.</param>
        /// <param name="postPerPage">Zorunlu değildir. Default olarak 50 kullanılır ve maksimum 50 girilebilir. Her sayfada kaç post getirileceğini belirtir.</param>
        /// <param name="pageCount">Zorunlu değildir. Kaç sayfalık post getirileceğini belirtir.</param>
        public List<Post> GetPostsFromUserId(string userId, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=003056d32c2554def87228bc3fd9668a&variables={\"id\":\"" + userId + "\",\"first\":" + postPerPage.ToString() + ",\"after\":\"" + after + "\"}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);

            cookies.ToList().ForEach(cookie =>
            {
                request.AddCookie(cookie.Key, cookie.Value);
            });

            IRestResponse response = client.Execute(request);
            Thread.Sleep(rnd.Next(3000, 5000));

            dynamic data = JObject.Parse(response.Content);
            var edges = data.data.user.edge_owner_to_timeline_media.edges;

            var posts = new List<Post>();
            foreach (var edge in edges)
            {
                var post = new Post();
                post.Id = edge.node.id;
                post.Picture = edge.node.display_url;
                post.Thumbnail = edge.node.thumbnail_src;
                post.Caption = (edge.node.edge_media_to_caption.edges as IList).Count > 0 ? edge.node.edge_media_to_caption.edges[0].node.text : "";
                post.isVideo = edge.node.is_video;
                post.ShortCode = edge.node.shortcode;
                post.CommentCount = edge.node.edge_media_to_comment.count;
                post.LikeCount = edge.node.edge_media_preview_like.count;
                post.Date = Tools.UnixTimeStampToDateTime((double)edge.node.taken_at_timestamp);
                posts.Add(post);
            }

            bool has_next_page = data.data.user.edge_owner_to_timeline_media.page_info.has_next_page;
            string end_cursor = data.data.user.edge_owner_to_timeline_media.page_info.end_cursor;

            if (!has_next_page || pageCount == 1)
                return posts;

            var nextPosts = GetPostsFromUserId(userId, cookies, postPerPage: postPerPage, pageCount: --pageCount, after: end_cursor);
            posts.AddRange(nextPosts);
            return posts;
        }

        /// <summary>
        /// Post'un altına yapılan yorumları getirir.
        /// </summary>
        /// <param name="shortcode">Zorunludur. Instagram'ı tarayıcıda açtığınızda https://www.instagram.com/p/X/ url'indeki X alanında yazan değerdir.</param>
        /// <param name="cookies">Zorunludur. Aktif cookie verileri tarayıcıdan alınarak girilebilir.</param>
        /// <param name="after">Zorunlu değildir. Sayfalama için kullanılır.</param>
        /// <param name="postPerPage">Zorunlu değildir. Default olarak 50 kullanılır ve maksimum 50 girilebilir. Her sayfada kaç post getirileceğini belirtir.</param>
        /// <param name="pageCount">Zorunlu değildir. Kaç sayfalık post getirileceğini belirtir.</param>
        public List<Comment> GetComments(string shortcode, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=bc3296d1ce80a24b1b6e40b1e72903f5&variables={\"shortcode\":\"" + shortcode + "\",\"first\":" + postPerPage.ToString() + ",\"after\":\"" + after?.Replace("\"", "%5C%22") + "\"}");
            client.Timeout = -1;
            client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36";
            var request = new RestRequest(Method.GET);

            cookies.ToList().ForEach(cookie =>
            {
                request.AddCookie(cookie.Key, cookie.Value);
            });

            IRestResponse response = client.Execute(request);

            Thread.Sleep(rnd.Next(3000, 5000));

            dynamic data = JObject.Parse(response.Content);

            bool has_next_page = data.data.shortcode_media.edge_media_to_parent_comment.page_info.has_next_page;
            string end_cursor = data.data.shortcode_media.edge_media_to_parent_comment.page_info.end_cursor;
            var edges = data.data.shortcode_media.edge_media_to_parent_comment.edges;

            var comments = new List<Comment>();

            foreach (var edge in edges)
            {
                var comment = new Comment();
                comment.Id = edge.node.id;
                comment.Text = edge.node.text;
                comment.Date = Tools.UnixTimeStampToDateTime((double)edge.node.created_at);
                comment.OwnerName = edge.node.owner.username;
                comment.OwnerPicture = edge.node.owner.profile_pic_url;
                comments.Add(comment);
            }

            if (!has_next_page || pageCount == 1)
                return comments;

            var nextComments = GetComments(shortcode, cookies, postPerPage: postPerPage, pageCount: --pageCount, after: end_cursor);
            comments.AddRange(nextComments);
            return comments;
        }
    }
}
