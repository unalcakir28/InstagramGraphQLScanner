using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InstagramAPI.Models;

namespace InstagramAPI.Services
{
    public interface IInstagramApiService
    {
        Task<User> GetUserAsync(string userName, Dictionary<string, string> cookies);
        Task<List<Post>> GetPostsFromTagAsync(string tag, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue);
        Task<List<Post>> GetPostsFromUserIdAsync(string userId, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue);
        Task<List<Comment>> GetCommentsAsync(string shortcode, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue);
    }
}