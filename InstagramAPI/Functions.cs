using InstagramAPI.Models;
using InstagramAPI.Logging;
using InstagramAPI.Exceptions;
using InstagramAPI.Configuration;
using InstagramAPI.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramAPI
{
    public class Functions : IInstagramApiService
    {
        private readonly Random _rnd = new Random();
        private readonly ILogger _logger;
        private readonly InstagramApiConfig _config;

        // JsonSerializerOptions yapılandırması
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public Functions(ILogger logger = null, InstagramApiConfig config = null)
        {
            _logger = logger ?? new ConsoleLogger();
            _config = config ?? InstagramApiConfig.Default;
        }

        private void LogApiCall(string endpoint, HttpStatusCode statusCode, string message)
        {
            _logger.LogInfo($"[{endpoint}] Status: {statusCode} - {message}");
        }

        private string CleanInstagramResponse(string response)
        {
            try
            {
                if (string.IsNullOrEmpty(response))
                {
                    throw new InstagramApiException("API yanıtı boş");
                }
                if (response.StartsWith("for (;;);"))
                {
                    response = response.Substring(9);
                }
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError("API yanıtı temizlenirken hata oluştu", ex);
                throw new InstagramApiException("API yanıtı temizlenirken hata oluştu", ex);
            }
        }

        private string GetCsrfToken(Dictionary<string, string> cookies)
        {
            if (cookies.TryGetValue("csrftoken", out string csrfToken))
            {
                return csrfToken;
            }
            return null;
        }

        private RestRequest CreateBaseRequest(Method method, Dictionary<string, string> cookies)
        {
            var request = new RestRequest(method);
            request.AddHeader("User-Agent", _config.UserAgent);
            request.AddHeader("Accept", "*/*");
            request.AddHeader("X-IG-App-ID", _config.AppId);
            request.AddHeader("X-ASBD-ID", "129477");
            request.AddHeader("X-IG-WWW-Claim", "0");

            string csrfToken = GetCsrfToken(cookies);
            if (!string.IsNullOrEmpty(csrfToken))
            {
                request.AddHeader("X-CSRFToken", csrfToken);
            }

            foreach (var cookie in cookies)
            {
                request.AddCookie(cookie.Key, cookie.Value);
            }

            return request;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string endpoint, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await Task.Delay(_rnd.Next(_config.MinRequestDelayMs, _config.MaxRequestDelayMs));
                    return await action();
                }
                catch (InstagramRateLimitException ex)
                {
                    _logger.LogWarning($"Rate limit hit on {endpoint}. Attempt {i + 1}/{maxRetries}");
                    if (i == maxRetries - 1) throw;
                    await Task.Delay(_config.RetryDelayMs * (i + 1));
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    _logger.LogWarning($"Request failed for {endpoint}. Attempt {i + 1}/{maxRetries}. Error: {ex.Message}");
                    await Task.Delay(_config.RetryDelayMs * (i + 1));
                }
            }
            throw new InstagramApiException($"Maximum retry attempts ({maxRetries}) reached for {endpoint}");
        }

        public async Task<User> GetUserAsync(string userName, Dictionary<string, string> cookies)
        {
            if (string.IsNullOrEmpty(userName))
                throw new ArgumentNullException(nameof(userName));
            if (cookies == null || !cookies.Any())
                throw new ArgumentException("Geçerli cookie bilgileri gerekli", nameof(cookies));

            return await ExecuteWithRetryAsync(async () =>
            {
                var endpoint = $"{_config.ApiVersion}/users/web_profile_info/?username={userName}";
                LogApiCall(endpoint, HttpStatusCode.Processing, $"Kullanıcı verisi isteniyor: {userName}");

                var client = new RestClient($"{_config.BaseUrl}/{endpoint}");
                var request = CreateBaseRequest(Method.GET, cookies);
                
                var response = await client.ExecuteAsync(request);
                LogApiCall(endpoint, response.StatusCode, $"API yanıtı alındı ({response.StatusCode})");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Kullanıcı bulunamadı: {userName}");
                    throw new InstagramApiException($"Kullanıcı bulunamadı: {userName}", endpoint, response.StatusCode);
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InstagramApiException($"API isteği başarısız: {response.StatusCode}", endpoint, response.StatusCode);
                }

                try
                {
                    var cleanedResponse = CleanInstagramResponse(response.Content);
                    _logger.LogDebug($"API Yanıtı: {cleanedResponse}");

                    using var jsonDoc = JsonDocument.Parse(cleanedResponse);
                    var root = jsonDoc.RootElement;

                    var userData = root.GetProperty("data").GetProperty("user");
                    if (userData.ValueKind == JsonValueKind.Null)
                    {
                        throw new InstagramAuthException("Kullanıcı bilgileri alınamadı. Instagram oturumu geçersiz olabilir.");
                    }

                    return new User
                    {
                        Id = userData.GetProperty("id").GetString(),
                        UserName = userData.GetProperty("username").GetString(),
                        FullName = userData.GetProperty("full_name").GetString(),
                        Biography = userData.TryGetProperty("biography", out var bio) ? bio.GetString() : "",
                        ProfilePicture = userData.GetProperty("profile_pic_url").GetString(),
                        ProfilePictureHD = userData.TryGetProperty("profile_pic_url_hd", out var hdPic) ? 
                            hdPic.GetString() : userData.GetProperty("profile_pic_url").GetString(),
                        FollowerCount = userData.GetProperty("edge_followed_by").GetProperty("count").GetInt32(),
                        FollowCount = userData.GetProperty("edge_follow").GetProperty("count").GetInt32(),
                        PostCount = userData.GetProperty("edge_owner_to_timeline_media").GetProperty("count").GetInt32()
                    };
                }
                catch (JsonException ex)
                {
                    _logger.LogError("JSON ayrıştırma hatası", ex);
                    if (response.Content.Contains("Please try closing and re-opening your browser window"))
                    {
                        throw new InstagramAuthException("Instagram oturumu geçersiz. Lütfen tarayıcıyı kapatıp açarak yeniden oturum açın.");
                    }
                    throw new InstagramApiException("Instagram API yanıtı ayrıştırılamadı", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Kullanıcı verisi işlenirken hata", ex);
                    throw new InstagramApiException("Instagram API yanıtı işlenirken hata oluştu", ex);
                }
            }, "GetUser");
        }

        public async Task<List<Post>> GetPostsFromTagAsync(string tag, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentNullException(nameof(tag));
            if (cookies == null || !cookies.Any())
                throw new ArgumentException("Geçerli cookie bilgileri gerekli", nameof(cookies));

            return await ExecuteWithRetryAsync(async () =>
            {
                var posts = new List<Post>();
                _logger.LogInfo($"Hashtag postları isteniyor: #{tag}, Sayfa: {pageCount}");

                // İlk olarak hashtag sayfasından genel bilgileri alalım
                var tagUrl = $"{_config.BaseUrl}/explore/tags/{tag}/";
                var tagClient = new RestClient(tagUrl);
                var tagRequest = CreateBaseRequest(Method.GET, cookies);

                _logger.LogDebug($"Hashtag sayfası yükleniyor: #{tag}...");
                var tagResponse = await tagClient.ExecuteAsync(tagRequest);

                if (tagResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new InstagramApiException($"Hashtag sayfasına erişilemedi", "explore/tags", tagResponse.StatusCode);
                }

                // API-v1 endpoint'ini kullanalım
                var endpoint = $"{_config.ApiVersion}/tags/logged_out_web_info/?tag_name={tag}";
                var client = new RestClient($"{_config.BaseUrl}/{endpoint}");
                var request = CreateBaseRequest(Method.GET, cookies);
                request.AddHeader("Referer", tagUrl);

                var response = await client.ExecuteAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning($"API yanıtı başarısız: {response.StatusCode}. Alternatif kaynak deneniyor...");
                    
                    // Alternatif olarak keşfet API'sini deneyelim
                    var exploreEndpoint = $"{_config.ApiVersion}/discover/web/explore_grid/";
                    var exploreClient = new RestClient($"{_config.BaseUrl}/{exploreEndpoint}");
                    var exploreRequest = CreateBaseRequest(Method.GET, cookies);
                    
                    var exploreResponse = await exploreClient.ExecuteAsync(exploreRequest);
                    
                    if (exploreResponse.StatusCode == HttpStatusCode.OK)
                    {
                        var cleanedExploreResponse = CleanInstagramResponse(exploreResponse.Content);
                        using var jsonDoc = JsonDocument.Parse(cleanedExploreResponse);
                        var root = jsonDoc.RootElement;
                        
                        _logger.LogInfo("Keşfet API'sinden rastgele postlar alınıyor...");
                        
                        if (root.TryGetProperty("sectional_items", out var sectionalItems) && 
                            sectionalItems.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var section in sectionalItems.EnumerateArray())
                            {
                                if (section.TryGetProperty("layout_content", out var layoutContent) && 
                                    layoutContent.TryGetProperty("medias", out var medias) &&
                                    medias.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var mediaItem in medias.EnumerateArray())
                                    {
                                        try
                                        {
                                            if (mediaItem.TryGetProperty("media", out var media))
                                            {
                                                posts.Add(ConvertToPost(media));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogDebug($"Post dönüştürme hatası: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (posts.Count > 0)
                    {
                        _logger.LogInfo($"Toplam {posts.Count} post bulundu (alternatif API'den)");
                        return posts;
                    }
                    
                    throw new InstagramApiException("Hashtag verileri alınamadı", endpoint, response.StatusCode);
                }

                try
                {
                    var cleanedResponse = CleanInstagramResponse(response.Content);
                    _logger.LogDebug($"Hashtag API yanıt özeti: {cleanedResponse.Substring(0, Math.Min(100, cleanedResponse.Length))}");
                    
                    using var jsonDoc = JsonDocument.Parse(cleanedResponse);
                    var root = jsonDoc.RootElement;
                    
                    if (!root.TryGetProperty("status", out var status) || status.GetString() != "ok")
                    {
                        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Bilinmeyen hata";
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + message);
                    }
                    
                    if (root.TryGetProperty("data", out var data))
                    {
                        // Öne çıkan ve son postları işle
                        ProcessMediaNodes(posts, data.TryGetProperty("top", out var top) ? 
                            top.TryGetProperty("sections", out var topSections) ? topSections : default : default);
                            
                        ProcessMediaNodes(posts, data.TryGetProperty("recent", out var recent) ? 
                            recent.TryGetProperty("sections", out var recentSections) ? recentSections : default : default);
                    }
                    
                    // HTML içinden veri çıkarma denemesi
                    if (posts.Count == 0)
                    {
                        _logger.LogInfo("API yanıtından post bulunamadı, HTML içeriği ayrıştırılıyor...");
                        
                        var match = Regex.Match(tagResponse.Content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                        if (match.Success)
                        {
                            using var htmlJsonDoc = JsonDocument.Parse(match.Groups[1].Value);
                            var htmlRoot = htmlJsonDoc.RootElement;
                            
                            if (htmlRoot.TryGetProperty("hashtag", out var hashtag) && 
                                hashtag.TryGetProperty("edge_hashtag_to_media", out var edgeHashtag) &&
                                edgeHashtag.TryGetProperty("edges", out var edges) &&
                                edges.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var edge in edges.EnumerateArray())
                                {
                                    try
                                    {
                                        if (edge.TryGetProperty("node", out var node))
                                        {
                                            posts.Add(ConvertToPost(node));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug($"HTML post dönüştürme hatası: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }

                    _logger.LogInfo($"Toplam {posts.Count} post bulundu");
                    return posts;
                }
                catch (JsonException ex)
                {
                    _logger.LogError("JSON ayrıştırma hatası", ex);
                    throw new InstagramApiException("Instagram hashtag API yanıtı ayrıştırılamadı", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Hashtag verisi işlenirken hata", ex);
                    throw new InstagramApiException("Instagram hashtag API yanıtı işlenemedi", ex);
                }
            }, "GetPostsFromTag");
        }

        private void ProcessMediaNodes(List<Post> posts, JsonElement sections)
        {
            if (sections.ValueKind != JsonValueKind.Array) return;

            foreach (var section in sections.EnumerateArray())
            {
                try
                {
                    if (section.TryGetProperty("media", out var mediaNode))
                    {
                        posts.Add(ConvertToPost(mediaNode));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Media node işleme hatası: {ex.Message}");
                }
            }
        }

        private Post ConvertToPost(JsonElement node)
        {
            var post = new Post
            {
                Id = node.TryGetProperty("id", out var id) ? id.GetString() : "",
                ShortCode = node.TryGetProperty("code", out var code) ? code.GetString() : 
                            node.TryGetProperty("shortcode", out var shortcode) ? shortcode.GetString() : "",
                Caption = node.TryGetProperty("caption", out var caption) ? 
                          (caption.TryGetProperty("text", out var text) ? text.GetString() : "") : "",
                isVideo = node.TryGetProperty("is_video", out var isVideo) ? isVideo.GetBoolean() : 
                          node.TryGetProperty("media_type", out var mediaType) ? (mediaType.GetInt32() == 2) : false,
                Picture = node.TryGetProperty("display_url", out var displayUrl) ? displayUrl.GetString() : 
                          node.TryGetProperty("image_versions2", out var imageVer) ? 
                          (imageVer.TryGetProperty("candidates", out var candidates) ? 
                          (candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0 ? 
                          candidates[0].TryGetProperty("url", out var url) ? url.GetString() : "" : "") : "") : "",
                Thumbnail = node.TryGetProperty("thumbnail_src", out var thumbSrc) ? thumbSrc.GetString() : 
                            node.TryGetProperty("image_versions2", out var imgVer) ? 
                            (imgVer.TryGetProperty("candidates", out var cands) ? 
                            (cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 1 ? 
                            cands[1].TryGetProperty("url", out var url2) ? url2.GetString() : 
                            (cands.GetArrayLength() > 0 ? cands[0].TryGetProperty("url", out var url3) ? 
                            url3.GetString() : "" : "") : "") : "") : "",
                CommentCount = node.TryGetProperty("comment_count", out var commentCount) ? commentCount.GetInt32() : 
                               node.TryGetProperty("edge_media_to_comment", out var edgeComment) ? 
                               edgeComment.TryGetProperty("count", out var count1) ? count1.GetInt32() : 0 : 0,
                LikeCount = node.TryGetProperty("like_count", out var likeCount) ? likeCount.GetInt32() : 
                            node.TryGetProperty("edge_liked_by", out var edgeLike) ? 
                            edgeLike.TryGetProperty("count", out var count2) ? count2.GetInt32() : 0 : 0
            };

            // Unix zaman damgasını işleme
            if (node.TryGetProperty("taken_at", out var takenAt))
                post.Date = Tools.UnixTimeStampToDateTime((double)takenAt.GetInt64());
            else if (node.TryGetProperty("taken_at_timestamp", out var timestamp))
                post.Date = Tools.UnixTimeStampToDateTime((double)timestamp.GetInt64());
            else
                post.Date = DateTime.Now;

            post.PostUrl = $"https://www.instagram.com/p/{post.ShortCode}";
            return post;
        }

        public async Task<List<Post>> GetPostsFromUserIdAsync(string userId, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentNullException(nameof(userId));
            if (cookies == null || !cookies.Any())
                throw new ArgumentException("Geçerli cookie bilgileri gerekli", nameof(cookies));

            return await ExecuteWithRetryAsync(async () =>
            {
                var endpoint = $"{_config.ApiVersion}/feed/user/{userId}/";
                _logger.LogInfo($"Kullanıcı postları isteniyor: {userId}, Sayfa: {pageCount}");

                var client = new RestClient($"{_config.BaseUrl}/{endpoint}");
                var request = CreateBaseRequest(Method.GET, cookies);

                // Parametreleri ekle
                request.AddQueryParameter("count", postPerPage.ToString());
                if (!string.IsNullOrEmpty(after))
                {
                    request.AddQueryParameter("max_id", after);
                }

                var response = await client.ExecuteAsync(request);
                LogApiCall(endpoint, response.StatusCode, $"API yanıtı alındı ({response.StatusCode})");

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InstagramApiException($"Kullanıcı postları alınamadı", endpoint, response.StatusCode);
                }

                try
                {
                    var cleanedResponse = CleanInstagramResponse(response.Content);
                    _logger.LogDebug($"Kullanıcı postları API yanıt özeti: {cleanedResponse.Substring(0, Math.Min(100, cleanedResponse.Length))}");

                    using var jsonDoc = JsonDocument.Parse(cleanedResponse);
                    var root = jsonDoc.RootElement;

                    if (!root.TryGetProperty("status", out var status) || status.GetString() != "ok")
                    {
                        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Bilinmeyen hata";
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + message);
                    }

                    var posts = new List<Post>();
                    if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            try
                            {
                                posts.Add(ConvertToPost(item));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"Post dönüştürme hatası: {ex.Message}");
                            }
                        }
                    }

                    // Sayfalama kontrolü
                    bool hasNextPage = root.TryGetProperty("more_available", out var moreAvailable) && moreAvailable.GetBoolean();
                    string nextCursor = root.TryGetProperty("next_max_id", out var nextMaxId) ? nextMaxId.GetString() : null;

                    if (hasNextPage && !string.IsNullOrEmpty(nextCursor) && pageCount > 1)
                    {
                        _logger.LogInfo($"Sonraki sayfa yükleniyor. Cursor: {nextCursor}");
                        var nextPosts = await GetPostsFromUserIdAsync(userId, cookies, nextCursor, postPerPage, --pageCount);
                        posts.AddRange(nextPosts);
                    }

                    _logger.LogInfo($"Toplam {posts.Count} post bulundu");
                    return posts;
                }
                catch (JsonException ex)
                {
                    _logger.LogError("JSON ayrıştırma hatası", ex);
                    throw new InstagramApiException("Instagram kullanıcı postları API yanıtı ayrıştırılamadı", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Kullanıcı postları işlenirken hata", ex);
                    throw new InstagramApiException("Instagram kullanıcı postları API yanıtı işlenemedi", ex);
                }
            }, "GetPostsFromUserId");
        }

        public async Task<List<Comment>> GetCommentsAsync(string shortcode, Dictionary<string, string> cookies, string after = null, int postPerPage = 50, int pageCount = int.MaxValue)
        {
            if (string.IsNullOrEmpty(shortcode))
                throw new ArgumentNullException(nameof(shortcode));
            if (cookies == null || !cookies.Any())
                throw new ArgumentException("Geçerli cookie bilgileri gerekli", nameof(cookies));

            return await ExecuteWithRetryAsync(async () =>
            {
                var comments = new List<Comment>();
                _logger.LogInfo($"Post yorumları isteniyor: {shortcode}, Sayfa: {pageCount}");

                // 1. Önce post sayfasından temel bilgileri alalım
                var postUrl = $"{_config.BaseUrl}/p/{shortcode}/";
                var client = new RestClient(postUrl);
                var request = CreateBaseRequest(Method.GET, cookies);

                _logger.LogDebug("Post sayfası yükleniyor...");
                var response = await client.ExecuteAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InstagramApiException($"Post sayfasına erişilemedi", "p/" + shortcode, response.StatusCode);
                }

                // 2. Media ID'yi bul
                string mediaId = await ExtractMediaIdAsync(response.Content);
                if (string.IsNullOrEmpty(mediaId))
                {
                    _logger.LogWarning("Media ID bulunamadı, HTML içeriğinden yorumlar çıkarılıyor...");
                    return await ExtractCommentsFromHtmlAsync(response.Content);
                }

                // 3. Yorumları API'den al
                var commentsEndpoint = $"{_config.ApiVersion}/media/{mediaId}/comments/";
                var commentsClient = new RestClient($"{_config.BaseUrl}/{commentsEndpoint}");
                var commentsRequest = CreateBaseRequest(Method.GET, cookies);

                // Parametreleri ekle
                commentsRequest.AddQueryParameter("can_support_threading", "true");
                commentsRequest.AddQueryParameter("permalink_enabled", "false");
                if (!string.IsNullOrEmpty(after))
                {
                    commentsRequest.AddQueryParameter("min_id", after);
                }

                var commentsResponse = await commentsClient.ExecuteAsync(commentsRequest);
                LogApiCall(commentsEndpoint, commentsResponse.StatusCode, $"API yanıtı alındı ({commentsResponse.StatusCode})");

                if (commentsResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new InstagramApiException($"Yorumlar alınamadı", commentsEndpoint, commentsResponse.StatusCode);
                }

                try
                {
                    var cleanedResponse = CleanInstagramResponse(commentsResponse.Content);
                    using var jsonDoc = JsonDocument.Parse(cleanedResponse);
                    var root = jsonDoc.RootElement;

                    if (!root.TryGetProperty("status", out var status) || status.GetString() != "ok")
                    {
                        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "Bilinmeyen hata";
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + message);
                    }

                    // Yorumları işle
                    if (root.TryGetProperty("comments", out var commentsArray) && commentsArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var commentItem in commentsArray.EnumerateArray())
                        {
                            try
                            {
                                comments.Add(new Comment
                                {
                                    Id = commentItem.GetProperty("pk").GetString(),
                                    Text = commentItem.GetProperty("text").GetString(),
                                    Date = Tools.UnixTimeStampToDateTime(commentItem.GetProperty("created_at").GetDouble()),
                                    OwnerName = commentItem.GetProperty("user").GetProperty("username").GetString(),
                                    OwnerPicture = commentItem.GetProperty("user").GetProperty("profile_pic_url").GetString()
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug($"Yorum dönüştürme hatası: {ex.Message}");
                            }
                        }
                    }

                    // Hiç yorum bulunamazsa
                    if (comments.Count == 0)
                    {
                        comments.Add(new Comment
                        {
                            Id = "info_" + DateTime.Now.Ticks,
                            Text = "Bu gönderide henüz hiç yorum yok veya yorumlar gizlenmiş olabilir.",
                            Date = DateTime.Now,
                            OwnerName = "system_info",
                            OwnerPicture = "https://instagram.com/favicon.ico"
                        });
                    }

                    // Sayfalama kontrolü
                    bool hasNextPage = root.TryGetProperty("has_more_comments", out var hasMoreComments) && hasMoreComments.GetBoolean();
                    string nextCursor = root.TryGetProperty("next_min_id", out var nextMinId) ? nextMinId.GetString() : null;

                    if (hasNextPage && !string.IsNullOrEmpty(nextCursor) && pageCount > 1)
                    {
                        _logger.LogInfo($"Sonraki sayfa yükleniyor. Cursor: {nextCursor}");
                        var nextComments = await GetCommentsAsync(shortcode, cookies, nextCursor, postPerPage, --pageCount);
                        comments.AddRange(nextComments);
                    }

                    _logger.LogInfo($"Toplam {comments.Count} yorum bulundu");
                    return comments;
                }
                catch (JsonException ex)
                {
                    _logger.LogError("JSON ayrıştırma hatası", ex);
                    throw new InstagramApiException("Instagram yorumlar API yanıtı ayrıştırılamadı", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Yorumlar işlenirken hata", ex);
                    throw new InstagramApiException("Instagram yorumlar API yanıtı işlenemedi", ex);
                }
            }, "GetComments");
        }

        private async Task<string> ExtractMediaIdAsync(string content)
        {
            try
            {
                // 1. Düzenli ifade ile dene
                var mediaIdMatch = Regex.Match(content, @"""media_id"":""(\d+)""");
                if (mediaIdMatch.Success)
                {
                    _logger.LogDebug($"Media ID düzenli ifade ile bulundu: {mediaIdMatch.Groups[1].Value}");
                    return mediaIdMatch.Groups[1].Value;
                }

                // 2. SharedData'dan dene
                var sharedDataMatch = Regex.Match(content, @"window\._sharedData\s*=\s*({.+?});</script>");
                if (sharedDataMatch.Success)
                {
                    using var jsonDoc = JsonDocument.Parse(sharedDataMatch.Groups[1].Value);
                    var root = jsonDoc.RootElement;
                    var mediaId = root.GetProperty("entry_data").GetProperty("PostPage")[0].GetProperty("graphql").GetProperty("shortcode_media").GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(mediaId))
                    {
                        _logger.LogDebug($"Media ID SharedData'dan bulundu: {mediaId}");
                        return mediaId;
                    }
                }

                // 3. AppData'dan dene
                var appDataMatch = Regex.Match(content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                if (appDataMatch.Success)
                {
                    using var jsonDoc = JsonDocument.Parse(appDataMatch.Groups[1].Value);
                    var root = jsonDoc.RootElement;
                    var mediaId = root.GetProperty("items")[0].GetProperty("id").GetString();
                    if (!string.IsNullOrEmpty(mediaId))
                    {
                        _logger.LogDebug($"Media ID AppData'dan bulundu: {mediaId}");
                        return mediaId;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Media ID çıkarılırken hata: {ex.Message}");
                return null;
            }
        }

        private async Task<List<Comment>> ExtractCommentsFromHtmlAsync(string content)
        {
            var comments = new List<Comment>();

            try
            {
                var commentDataMatches = Regex.Matches(content, @"""text"":""([^""\\]*(?:\\.[^""\\]*)*)"",""created_at"":(\d+\.?\d*),""owner"":{""id"":""(\d+)"",""profile_pic_url"":""([^""]+)"",""username"":""([^""]+)""}");

                foreach (Match match in commentDataMatches)
                {
                    try
                    {
                        var commentText = Regex.Unescape(match.Groups[1].Value);
                        var createdAt = double.Parse(match.Groups[2].Value);
                        var ownerId = match.Groups[3].Value;
                        var ownerPic = match.Groups[4].Value;
                        var ownerName = match.Groups[5].Value;

                        comments.Add(new Comment
                        {
                            Id = $"{ownerId}_{createdAt}",
                            Text = commentText,
                            Date = Tools.UnixTimeStampToDateTime(createdAt),
                            OwnerName = ownerName,
                            OwnerPicture = ownerPic
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"HTML yorum dönüştürme hatası: {ex.Message}");
                    }
                }

                if (comments.Count == 0)
                {
                    comments.Add(new Comment
                    {
                        Id = "info_" + DateTime.Now.Ticks,
                        Text = "Bu gönderiye ait yorum bulunamadı veya yorumlar kısıtlanmış durumda.",
                        Date = DateTime.Now,
                        OwnerName = "system_info",
                        OwnerPicture = "https://instagram.com/favicon.ico"
                    });
                }

                _logger.LogInfo($"{comments.Count} yorum HTML içeriğinden çıkarıldı");
            }
            catch (Exception ex)
            {
                _logger.LogError("HTML'den yorumlar çıkarılırken hata", ex);
                comments.Add(new Comment
                {
                    Id = "error_" + DateTime.Now.Ticks,
                    Text = "Yorumlar alınırken bir hata oluştu: " + ex.Message,
                    Date = DateTime.Now,
                    OwnerName = "system_error",
                    OwnerPicture = "https://instagram.com/favicon.ico"
                });
            }

            return comments;
        }
    }
}
