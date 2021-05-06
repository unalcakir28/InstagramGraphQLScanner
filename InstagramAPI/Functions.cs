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

        public User GetUser(string userName)
        {
            var client = new RestClient("https://www.instagram.com/" + userName + "/?__a=1");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
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

        public List<Post> GetPostsFromTag(string tag, int pageCount, string after = null)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=9b498c08113f1e09617a1703c22b2f32&variables={\"tag_name\":\"" + tag + "\",\"first\":50,\"after\":\"" + after + "\"}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
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

            var nextPosts = GetPostsFromTag(tag, --pageCount, end_cursor);
            posts.AddRange(nextPosts);
            return posts;
        }

        public List<Post> GetPostsFromUserId(string userId, string after = null)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=003056d32c2554def87228bc3fd9668a&variables={\"id\":\"" + userId + "\",\"first\":50,\"after\":\"" + after + "\"}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
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

            if (!has_next_page)
                return posts;

            var nextPosts = GetPostsFromUserId(userId, end_cursor);
            posts.AddRange(nextPosts);
            return posts;
        }

        public List<Comment> GetComments(string shortcode, string after = null, Dictionary<string, string> cookies = null)
        {
            var client = new RestClient("https://www.instagram.com/graphql/query/?query_hash=bc3296d1ce80a24b1b6e40b1e72903f5&variables={\"shortcode\":\"" + shortcode + "\",\"first\":50,\"after\":\"" + after?.Replace("\"", "%5C%22") + "\"}");
            client.Timeout = -1;
            client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36";
            var request = new RestRequest(Method.GET);

            if (cookies != null)
            {
                cookies.ToList().ForEach(cookie =>
                {
                    request.AddCookie(cookie.Key, cookie.Value);
                });
            }

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

            if (!has_next_page)
                return comments;

            var nextComments = GetComments(shortcode, end_cursor, cookies);
            comments.AddRange(nextComments);
            return comments;
        }
    }
}
