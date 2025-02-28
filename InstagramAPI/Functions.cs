using InstagramAPI.Models;
using InstagramAPI.Logging;
using InstagramAPI.Exceptions;
using InstagramAPI.Configuration;
using InstagramAPI.Services;
using Newtonsoft.Json.Linq;
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

                    dynamic data = JObject.Parse(cleanedResponse);
                    var userData = data.data.user;

                    if (userData == null)
                    {
                        throw new InstagramAuthException("Kullanıcı bilgileri alınamadı. Instagram oturumu geçersiz olabilir.");
                    }

                    return new User
                    {
                        Id = userData.id,
                        UserName = userData.username,
                        FullName = userData.full_name,
                        Biography = userData.biography,
                        ProfilePicture = userData.profile_pic_url,
                        ProfilePictureHD = userData.profile_pic_url_hd ?? userData.profile_pic_url,
                        FollowerCount = userData.edge_followed_by.count,
                        FollowCount = userData.edge_follow.count,
                        PostCount = userData.edge_owner_to_timeline_media.count
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError("JSON ayrıştırma hatası", ex);
                    if (response.Content.Contains("Please try closing and re-opening your browser window"))
                    {
                        throw new InstagramAuthException("Instagram oturumu geçersiz. Lütfen tarayıcıyı kapatıp açarak yeniden oturum açın.");
                    }
                    throw new InstagramApiException("Instagram API yanıtı ayrıştırılamadı", ex);
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
                        dynamic exploreData = JObject.Parse(cleanedExploreResponse);
                        
                        _logger.LogInfo("Keşfet API'sinden rastgele postlar alınıyor...");
                        
                        if (exploreData.sectional_items != null)
                        {
                            foreach (var section in exploreData.sectional_items)
                            {
                                if (section.layout_content?.medias != null)
                                {
                                    foreach (var mediaItem in section.layout_content.medias)
                                    {
                                        try
                                        {
                                            posts.Add(ConvertToPost(mediaItem.media));
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
                    
                    dynamic data = JObject.Parse(cleanedResponse);
                    
                    if (data.status != "ok")
                    {
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + data.message);
                    }
                    
                    if (data.data != null)
                    {
                        // Öne çıkan ve son postları işle
                        ProcessMediaNodes(posts, data.data.top?.sections);
                        ProcessMediaNodes(posts, data.data.recent?.sections);
                    }
                    
                    // HTML içinden veri çıkarma denemesi
                    if (posts.Count == 0)
                    {
                        _logger.LogInfo("API yanıtından post bulunamadı, HTML içeriği ayrıştırılıyor...");
                        
                        var match = Regex.Match(tagResponse.Content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                        if (match.Success)
                        {
                            dynamic htmlData = JObject.Parse(match.Groups[1].Value);
                            if (htmlData.hashtag?.edge_hashtag_to_media?.edges != null)
                            {
                                foreach (var edge in htmlData.hashtag.edge_hashtag_to_media.edges)
                                {
                                    try
                                    {
                                        posts.Add(ConvertToPost(edge.node));
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
                catch (Exception ex)
                {
                    _logger.LogError("Hashtag verisi işlenirken hata", ex);
                    throw new InstagramApiException("Instagram hashtag API yanıtı işlenemedi", ex);
                }
            }, "GetPostsFromTag");
        }

        private void ProcessMediaNodes(List<Post> posts, dynamic sections)
        {
            if (sections == null) return;

            foreach (var section in sections)
            {
                try
                {
                    var mediaNode = section.media;
                    posts.Add(ConvertToPost(mediaNode));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Media node işleme hatası: {ex.Message}");
                }
            }
        }

        private Post ConvertToPost(dynamic node)
        {
            var post = new Post
            {
                Id = node.id,
                ShortCode = node.code ?? node.shortcode,
                Caption = node.caption?.text ?? "",
                isVideo = node.is_video ?? (node.media_type == 2),
                Picture = node.display_url ?? node.image_versions2?.candidates[0]?.url,
                Thumbnail = node.thumbnail_src ?? node.image_versions2?.candidates[1]?.url ?? node.image_versions2?.candidates[0]?.url,
                CommentCount = node.comment_count ?? node.edge_media_to_comment?.count ?? 0,
                LikeCount = node.like_count ?? node.edge_liked_by?.count ?? 0,
                Date = Tools.UnixTimeStampToDateTime((double)(node.taken_at ?? node.taken_at_timestamp)),
            };
            
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

                    dynamic data = JObject.Parse(cleanedResponse);

                    if (data.status != "ok")
                    {
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + data.message);
                    }

                    var posts = new List<Post>();
                    if (data.items != null)
                    {
                        foreach (var item in data.items)
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
                    bool hasNextPage = data.more_available == true;
                    string nextCursor = data.next_max_id?.ToString();

                    if (hasNextPage && !string.IsNullOrEmpty(nextCursor) && pageCount > 1)
                    {
                        _logger.LogInfo($"Sonraki sayfa yükleniyor. Cursor: {nextCursor}");
                        var nextPosts = await GetPostsFromUserIdAsync(userId, cookies, nextCursor, postPerPage, --pageCount);
                        posts.AddRange(nextPosts);
                    }

                    _logger.LogInfo($"Toplam {posts.Count} post bulundu");
                    return posts;
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
                    dynamic data = JObject.Parse(cleanedResponse);

                    if (data.status != "ok")
                    {
                        throw new InstagramApiException("Instagram API yanıtı başarısız: " + data.message);
                    }

                    // Yorumları işle
                    if (data.comments != null)
                    {
                        foreach (var commentItem in data.comments)
                        {
                            try
                            {
                                comments.Add(new Comment
                                {
                                    Id = commentItem.pk,
                                    Text = commentItem.text,
                                    Date = Tools.UnixTimeStampToDateTime((double)commentItem.created_at),
                                    OwnerName = commentItem.user.username,
                                    OwnerPicture = commentItem.user.profile_pic_url
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
                    bool hasNextPage = data.has_more_comments == true;
                    string nextCursor = data.next_min_id?.ToString();

                    if (hasNextPage && !string.IsNullOrEmpty(nextCursor) && pageCount > 1)
                    {
                        _logger.LogInfo($"Sonraki sayfa yükleniyor. Cursor: {nextCursor}");
                        var nextComments = await GetCommentsAsync(shortcode, cookies, nextCursor, postPerPage, --pageCount);
                        comments.AddRange(nextComments);
                    }

                    _logger.LogInfo($"Toplam {comments.Count} yorum bulundu");
                    return comments;
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
                    dynamic sharedData = JObject.Parse(sharedDataMatch.Groups[1].Value);
                    var mediaId = sharedData.entry_data?.PostPage[0]?.graphql?.shortcode_media?.id;
                    if (!string.IsNullOrEmpty(mediaId?.ToString()))
                    {
                        _logger.LogDebug($"Media ID SharedData'dan bulundu: {mediaId}");
                        return mediaId.ToString();
                    }
                }

                // 3. AppData'dan dene
                var appDataMatch = Regex.Match(content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                if (appDataMatch.Success)
                {
                    dynamic appData = JObject.Parse(appDataMatch.Groups[1].Value);
                    var mediaId = appData.items?[0]?.id;
                    if (!string.IsNullOrEmpty(mediaId?.ToString()))
                    {
                        _logger.LogDebug($"Media ID AppData'dan bulundu: {mediaId}");
                        return mediaId.ToString();
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
