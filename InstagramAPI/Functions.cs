using InstagramAPI.Models;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramAPI
{
    public class Functions
    {
        Random rnd = new Random();

        // Instagram API yanıtlarını temizleyen yardımcı metot
        private string CleanInstagramResponse(string response)
        {
            // "for (;;);" veya diğer önekleri temizle
            if (response.StartsWith("for (;;);"))
            {
                response = response.Substring(9);
            }
            return response;
        }

        // Cookies içinden CSRF token'ı çıkaran yardımcı metot
        private string GetCsrfToken(Dictionary<string, string> cookies)
        {
            if (cookies.TryGetValue("csrftoken", out string csrfToken))
            {
                return csrfToken;
            }
            return null;
        }

        // Güvenli bir kullanıcı aracısı döndüren yardımcı metot
        private string GetUserAgent()
        {
            return "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";
        }

        /// <summary>
        /// Kullanıcı adı ile instagram veritabanındaki public kullanıcı verilerini getirir.
        /// </summary>
        /// <param name="userName">Zorunludur. Verileri getirilecek kullanıcının instagram kullanıcı adı girilmelidir. https://www.instagram.com/X/ url'indeki X alanında yazan değerdir</param>
        /// <param name="cookies">Zorunludur. Aktif cookie verileri tarayıcıdan alınarak girilebilir.</param>
        public User GetUser(string userName, Dictionary<string, string> cookies)
        {
            Console.WriteLine($"Kullanıcı verisi isteniyor: {userName}");
            
            // Son Instagram API değişikliklerinde farklı endpoint kullanılmaya başlandı
            var client = new RestClient($"https://www.instagram.com/api/v1/users/web_profile_info/?username={userName}");
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            
            // Gerekli HTTP başlıkları ekle
            request.AddHeader("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("X-IG-App-ID", "936619743392459"); // Instagram web uygulamasının App ID'si
            request.AddHeader("X-ASBD-ID", "129477");
            request.AddHeader("X-IG-WWW-Claim", "0");
            
            foreach (var cookie in cookies)
            {
                request.AddCookie(cookie.Key, cookie.Value);
            }
            
            IRestResponse response = client.Execute(request);
            Thread.Sleep(rnd.Next(3000, 5000));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new Exception("Kullanıcı bulunamadı.");

            // Hata yakalama ve debug için yanıtı kontrol et
            Console.WriteLine("API Yanıt Kodu: " + response.StatusCode);
            Console.WriteLine("API Yanıt Başlıkları: " + string.Join(", ", response.Headers.Select(h => $"{h.Name}: {h.Value}")));
            
            try {
                var cleanedResponse = CleanInstagramResponse(response.Content);
                Console.WriteLine("Temizlenmiş API Yanıtı: " + cleanedResponse);
                
                dynamic data = JObject.Parse(cleanedResponse);
                
                // Yeni API yanıt yapısı, farklı bir JSON şemasına sahip
                var userData = data.data.user;
                
                if (userData == null)
                {
                    throw new Exception("Kullanıcı bilgileri alınamadı. Instagram oturumu geçersiz olabilir.");
                }
                
                var user = new User();
                user.Id = userData.id;
                user.UserName = userData.username;
                user.FullName = userData.full_name;
                user.Biography = userData.biography;
                user.ProfilePicture = userData.profile_pic_url;
                user.ProfilePictureHD = userData.profile_pic_url_hd ?? userData.profile_pic_url;
                user.FollowerCount = userData.edge_followed_by.count;
                user.FollowCount = userData.edge_follow.count;
                user.PostCount = userData.edge_owner_to_timeline_media.count;
                return user;
            }
            catch (Exception ex) {
                Console.WriteLine("JSON ayrıştırma hatası: " + ex.Message);
                
                // Yeniden oturum açmanın gerekli olduğunu belirten bir hata fırlat
                if (response.Content.Contains("errorSummary") && response.Content.Contains("Please try closing and re-opening your browser window"))
                {
                    throw new Exception("Instagram oturumu geçersiz. Lütfen tarayıcıyı kapatıp açarak yeniden oturum açın.");
                }
                
                throw new Exception("Instagram API yanıtı ayrıştırılamadı. Instagram API yapısı değişmiş olabilir: " + ex.Message);
            }
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
            Console.WriteLine($"Hashtag postları isteniyor: {tag}, Sayfa: {pageCount}");
            
            try
            {
                // İlk olarak hashtag sayfasından öneri postları ve genel bilgileri alalım
                var tagUrl = $"https://www.instagram.com/explore/tags/{tag}/";
                var tagClient = new RestClient(tagUrl);
                var tagRequest = new RestRequest(Method.GET);
            
                // Tarayıcı gibi davranmak için gereken başlıkları ekle
                tagRequest.AddHeader("User-Agent", GetUserAgent());
                tagRequest.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                tagRequest.AddHeader("Accept-Language", "en-US,en;q=0.9");

                // Çerezleri ekle
                foreach (var cookie in cookies)
                {
                    tagRequest.AddCookie(cookie.Key, cookie.Value);
                }

                Console.WriteLine($"Hashtag sayfası yükleniyor: #{tag}...");
                IRestResponse tagResponse = tagClient.Execute(tagRequest);
                Thread.Sleep(rnd.Next(2000, 4000));

                if (tagResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"Hashtag sayfasına erişilemedi: {tagResponse.StatusCode}");
                }

                // GrapQL API kullanarak daha fazla post yükleyelim
                var posts = new List<Post>();
                
                // API-v1 kullanımına geçelim - çalışan bir API endpoint'i
                var url = $"https://www.instagram.com/api/v1/tags/logged_out_web_info/?tag_name={tag}";
                var client = new RestClient(url);
                client.Timeout = -1;
                var request = new RestRequest(Method.GET);

                // CSRF token'ı ekle (önemli)
                string csrfToken = GetCsrfToken(cookies);
                
                // Gerekli HTTP başlıkları ekle
                request.AddHeader("User-Agent", GetUserAgent());
                request.AddHeader("Accept", "*/*");
                request.AddHeader("X-IG-App-ID", "936619743392459");
                request.AddHeader("X-ASBD-ID", "129477");
                request.AddHeader("X-IG-WWW-Claim", "0");
                request.AddHeader("X-Requested-With", "XMLHttpRequest");
                request.AddHeader("X-CSRFToken", csrfToken);
                request.AddHeader("Referer", $"https://www.instagram.com/explore/tags/{tag}/");
                
                // Çerezleri ekle
                foreach (var cookie in cookies)
                {
                    request.AddCookie(cookie.Key, cookie.Value);
                }
                
                IRestResponse response = client.Execute(request);
                Thread.Sleep(rnd.Next(2000, 4000));

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Hata kodu: {response.StatusCode}, Yanıt: {response.Content}");
                    
                    // Test için başka bir endpoint deneyelim - keşfet sayfasından postlar alalım
                    Console.WriteLine("Alternatif kaynak deneniyor: Keşfet API'si");
                    var exploreUrl = "https://www.instagram.com/api/v1/discover/web/explore_grid/";
                    var exploreClient = new RestClient(exploreUrl);
                    var exploreRequest = new RestRequest(Method.GET);
                    
                    // Aynı başlıkları ekle
                    exploreRequest.AddHeader("User-Agent", GetUserAgent());
                    exploreRequest.AddHeader("Accept", "*/*");
                    exploreRequest.AddHeader("X-IG-App-ID", "936619743392459");
                    exploreRequest.AddHeader("X-ASBD-ID", "129477");
                    exploreRequest.AddHeader("X-CSRFToken", csrfToken);
                    exploreRequest.AddHeader("X-Requested-With", "XMLHttpRequest");
                    
                    // Çerezleri ekle
                    foreach (var cookie in cookies)
                    {
                        exploreRequest.AddCookie(cookie.Key, cookie.Value);
                    }
                    
                    // Keşfet API'sinden yanıt al
                    IRestResponse exploreResponse = exploreClient.Execute(exploreRequest);
                    
                    if (exploreResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var cleanedExploreResponse = CleanInstagramResponse(exploreResponse.Content);
                        dynamic exploreData = JObject.Parse(cleanedExploreResponse);
                        
                        Console.WriteLine("Keşfet API'sinden rastgele postlar gösteriliyor...");
                        
                        // Keşfet sayfasından içerik ekleyelim
                        if (exploreData.sectional_items != null && exploreData.sectional_items.Count > 0)
                        {
                            foreach (var section in exploreData.sectional_items)
                            {
                                if (section.layout_content != null && section.layout_content.medias != null)
                                {
                                    foreach (var mediaItem in section.layout_content.medias)
                                    {
                                        try
                                        {
                                            var mediaNode = mediaItem.media;
                                            var post = new Post();
                                            post.Id = mediaNode.id;
                                            post.ShortCode = mediaNode.code;
                                            post.Caption = mediaNode.caption?.text ?? "(Hashtag araması şu anda kısıtlandı. Rastgele içerik gösteriliyor)";
                                            post.isVideo = mediaNode.media_type == 2; // 2 = video, 1 = image
                                            
                                            if (mediaNode.image_versions2 != null && mediaNode.image_versions2.candidates != null && mediaNode.image_versions2.candidates.Count > 0)
                                            {
                                                post.Picture = mediaNode.image_versions2.candidates[0].url;
                                                post.Thumbnail = mediaNode.image_versions2.candidates[1]?.url ?? mediaNode.image_versions2.candidates[0].url;
                                            }
                                            
                                            post.CommentCount = mediaNode.comment_count ?? 0;
                                            post.LikeCount = mediaNode.like_count ?? 0;
                                            post.Date = Tools.UnixTimeStampToDateTime((double)mediaNode.taken_at);
                                            post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                                            posts.Add(post);
                                        }
                                        catch (Exception) { /* Hatalı medyayı atla */ }
                                    }
                                }
                            }
                        }
                    }
                    
                    if (posts.Count > 0)
                    {
                        Console.WriteLine($"Toplam {posts.Count} post bulundu (alternatif API'den)");
                        return posts;
                    }
                    
                    throw new Exception($"Instagram API hatası: {response.StatusCode} - Hashtag verileri alınamadı");
                }

                try {
                    var cleanedResponse = CleanInstagramResponse(response.Content);
                    Console.WriteLine("Hashtag API yanıt özeti: " + cleanedResponse.Substring(0, Math.Min(100, cleanedResponse.Length)));
                    
                    dynamic data = JObject.Parse(cleanedResponse);
                    
                    // Hata kontrolü
                    if (data.status != "ok")
                    {
                        throw new Exception("Instagram API yanıtı başarısız: " + data.message);
                    }
                    
                    // Top ve Recent postları çek
                    if (data.data != null)
                    {
                        // Öne çıkan postlar
                        if (data.data.top != null && data.data.top.sections != null)
                        {
                            foreach (var section in data.data.top.sections)
                            {
                                try 
                                {
                                    var mediaNode = section.media;
                                    var post = new Post();
                                    post.Id = mediaNode.id;
                                    post.ShortCode = mediaNode.code;
                                    post.Caption = mediaNode.caption?.text ?? "";
                                    post.isVideo = mediaNode.is_video;
                                    post.Picture = mediaNode.display_url;
                                    post.Thumbnail = mediaNode.thumbnail_src ?? mediaNode.display_url;
                                    post.CommentCount = mediaNode.comment_count;
                                    post.LikeCount = mediaNode.like_count;
                                    post.Date = Tools.UnixTimeStampToDateTime((double)mediaNode.taken_at);
                                    post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                                    posts.Add(post);
                                }
                                catch (Exception) { /* Hatalı medyayı atla */ }
                            }
                        }
                        
                        // Son postlar
                        if (data.data.recent != null && data.data.recent.sections != null)
                        {
                            foreach (var section in data.data.recent.sections)
                            {
                                try
                                {
                                    var mediaNode = section.media;
                                    var post = new Post();
                                    post.Id = mediaNode.id;
                                    post.ShortCode = mediaNode.code;
                                    post.Caption = mediaNode.caption?.text ?? "";
                                    post.isVideo = mediaNode.is_video;
                                    post.Picture = mediaNode.display_url;
                                    post.Thumbnail = mediaNode.thumbnail_src ?? mediaNode.display_url;
                                    post.CommentCount = mediaNode.comment_count;
                                    post.LikeCount = mediaNode.like_count;
                                    post.Date = Tools.UnixTimeStampToDateTime((double)mediaNode.taken_at);
                                    post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                                    posts.Add(post);
                                }
                                catch (Exception) { /* Hatalı medyayı atla */ }
                            }
                        }
                    }
                    
                    // Eğer içerik bulamazsak HTML içerisinden veri çekmeyi deneyelim
                    if (posts.Count == 0)
                    {
                        Console.WriteLine("API yanıtından post bulunamadı, HTML içeriği ayrıştırılıyor...");
                        
                        // HTML içerisinden JSON veri çıkar
                        var content = tagResponse.Content;
                        var match = Regex.Match(content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                        
                        if (match.Success)
                        {
                            string jsonData = match.Groups[1].Value;
                            try
                            {
                                dynamic htmlData = JObject.Parse(jsonData);
                                
                                // Veriyi işle
                                if (htmlData.hashtag != null && 
                                    htmlData.hashtag.edge_hashtag_to_media != null && 
                                    htmlData.hashtag.edge_hashtag_to_media.edges != null)
                                {
                                    foreach (var edge in htmlData.hashtag.edge_hashtag_to_media.edges)
                                    {
                                        try
                                        {
                                            var node = edge.node;
                                            var post = new Post();
                                            post.Id = node.id;
                                            post.ShortCode = node.shortcode;
                                            post.Caption = node.edge_media_to_caption?.edges[0]?.node.text ?? "";
                                            post.isVideo = node.is_video;
                                            post.Picture = node.display_url;
                                            post.Thumbnail = node.thumbnail_src;
                                            post.CommentCount = node.edge_media_to_comment?.count ?? 0;
                                            post.LikeCount = node.edge_liked_by?.count ?? 0;
                                            post.Date = Tools.UnixTimeStampToDateTime((double)node.taken_at_timestamp);
                                            post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                                            posts.Add(post);
                                        }
                                        catch (Exception) { /* Hatalı düğümü atla */ }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("HTML ayrıştırma hatası: " + ex.Message);
                            }
                        }
                    }
                    
                    Console.WriteLine($"Toplam {posts.Count} post bulundu");
                    return posts;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Hashtag API hatası: " + ex.Message);
                    throw new Exception("Instagram hashtag API yanıtı işlenemedi: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Hashtag API hatası: " + ex.Message);
                throw new Exception("Hashtag verileri alınamadı: " + ex.Message);
            }
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
            Console.WriteLine($"Kullanıcı postları isteniyor: {userId}, Sayfa: {pageCount}");
            
            // Güncel Instagram kullanıcı medya API endpoint'i
            var url = $"https://www.instagram.com/api/v1/feed/user/{userId}/";
            var client = new RestClient(url);
            client.Timeout = -1;
            var request = new RestRequest(Method.GET);
            
            // Gerekli HTTP başlıkları ekle
            request.AddHeader("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            request.AddHeader("Accept", "*/*");
            request.AddHeader("X-IG-App-ID", "936619743392459");
            request.AddHeader("X-ASBD-ID", "129477");
            request.AddHeader("X-IG-WWW-Claim", "0");
            
            // Parametreleri ekle
            request.AddQueryParameter("count", postPerPage.ToString());
            if (!string.IsNullOrEmpty(after))
            {
                request.AddQueryParameter("max_id", after);
            }
            
            foreach (var cookie in cookies)
            {
                request.AddCookie(cookie.Key, cookie.Value);
            }
            
            IRestResponse response = client.Execute(request);
            Thread.Sleep(rnd.Next(3000, 5000));

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"Hata kodu: {response.StatusCode}, Yanıt: {response.Content}");
                throw new Exception($"Instagram API hatası: {response.StatusCode}");
            }

            try {
                var cleanedResponse = CleanInstagramResponse(response.Content);
                Console.WriteLine("Kullanıcı postları API yanıt özeti: " + cleanedResponse.Substring(0, Math.Min(100, cleanedResponse.Length)));
                
                dynamic data = JObject.Parse(cleanedResponse);
                
                // Hata kontrolü
                if (data.status != "ok")
                {
                    throw new Exception("Instagram API yanıtı başarısız: " + data.message);
                }
                
                var posts = new List<Post>();
                var items = data.items;
                
                foreach (var item in items)
                {
                    var post = new Post();
                    post.Id = item.id;
                    
                    if (item.image_versions2 != null && item.image_versions2.candidates != null && item.image_versions2.candidates.Count > 0)
                    {
                        post.Picture = item.image_versions2.candidates[0].url;
                        post.Thumbnail = item.image_versions2.candidates[1]?.url ?? item.image_versions2.candidates[0].url;
                    }
                    else if (item.carousel_media != null && item.carousel_media.Count > 0)
                    {
                        // Carousel post (çoklu medya)
                        var firstMedia = item.carousel_media[0];
                        if (firstMedia.image_versions2 != null && firstMedia.image_versions2.candidates != null && firstMedia.image_versions2.candidates.Count > 0)
                        {
                            post.Picture = firstMedia.image_versions2.candidates[0].url;
                            post.Thumbnail = firstMedia.image_versions2.candidates[1]?.url ?? firstMedia.image_versions2.candidates[0].url;
                        }
                    }
                    
                    post.Caption = item.caption?.text ?? "";
                    post.isVideo = item.media_type == 2; // 2 = video, 1 = image, 8 = carousel
                    post.ShortCode = item.code;
                    post.CommentCount = item.comment_count;
                    post.LikeCount = item.like_count;
                    post.Date = Tools.UnixTimeStampToDateTime((double)item.taken_at);
                    post.PostUrl = "https://www.instagram.com/p/" + post.ShortCode;
                    posts.Add(post);
                }
                
                // Pagination kontrolü
                bool has_next_page = data.more_available == true;
                string end_cursor = data.next_max_id?.ToString();
                
                if (!has_next_page || string.IsNullOrEmpty(end_cursor) || pageCount == 1)
                    return posts;
                
                var nextPosts = GetPostsFromUserId(userId, cookies, postPerPage: postPerPage, pageCount: --pageCount, after: end_cursor);
                posts.AddRange(nextPosts);
                return posts;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Kullanıcı postları API hatası: " + ex.Message);
                throw new Exception("Instagram kullanıcı postları API yanıtı işlenemedi: " + ex.Message);
            }
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
            Console.WriteLine($"Post yorumları isteniyor: {shortcode}, Sayfa: {pageCount}");
            var comments = new List<Comment>();
            
            try
            {
                // 1. Önce post sayfasına gidelim ve temel içeriği alalım
                var postUrl = $"https://www.instagram.com/p/{shortcode}/";
                var client = new RestClient(postUrl);
                var request = new RestRequest(Method.GET);
                
                // Tarayıcı gibi davranmak için gereken başlıkları ekle
                request.AddHeader("User-Agent", GetUserAgent());
                request.AddHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                request.AddHeader("Accept-Language", "en-US,en;q=0.9");
                
                foreach (var cookie in cookies)
                {
                    request.AddCookie(cookie.Key, cookie.Value);
                }
                
                Console.WriteLine("Post sayfası yükleniyor...");
                IRestResponse response = client.Execute(request);
                Thread.Sleep(rnd.Next(2000, 4000));
                
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"Post sayfasına erişilemedi: {response.StatusCode}");
                }
                
                // 2. Medya ID'sini HTML içeriğinden çıkaralım
                var content = response.Content;
                string mediaId = null;
                
                // Düzenli ifadelerle medya ID'sini bulmaya çalışalım
                var mediaIdMatch = Regex.Match(content, @"""media_id"":""(\d+)""");
                if (mediaIdMatch.Success)
                {
                    mediaId = mediaIdMatch.Groups[1].Value;
                    Console.WriteLine($"HTML içeriğinden medya ID bulundu: {mediaId}");
                }
                else
                {
                    // Alternative approach to find media ID - using shared_data
                    var sharedDataMatch = Regex.Match(content, @"window\._sharedData\s*=\s*({.+?});</script>");
                    if (sharedDataMatch.Success)
                    {
                        try
                        {
                            dynamic sharedData = JObject.Parse(sharedDataMatch.Groups[1].Value);
                            mediaId = sharedData.entry_data?.PostPage[0]?.graphql?.shortcode_media?.id;
                            Console.WriteLine($"SharedData içeriğinden medya ID bulundu: {mediaId}");
                        }
                        catch { /* Parse hatalarını görmezden gel */ }
                    }
                    
                    // Another alternative approach to find media ID
                    if (string.IsNullOrEmpty(mediaId))
                    {
                        var appDataMatch = Regex.Match(content, @"<script type=""application/json"" data-sj>(.*?)</script>");
                        if (appDataMatch.Success)
                        {
                            try
                            {
                                dynamic appData = JObject.Parse(appDataMatch.Groups[1].Value);
                                mediaId = appData.items?[0]?.id;
                                Console.WriteLine($"AppData içeriğinden medya ID bulundu: {mediaId}");
                            }
                            catch { /* Parse hatalarını görmezden gel */ }
                        }
                    }
                }
                
                // 3. Eğer medya ID bulunamazsa bazı yorumları HTML'den çıkaralım
                if (string.IsNullOrEmpty(mediaId))
                {
                    Console.WriteLine("Medya ID bulunamadı. HTML içeriğinden yorumlar çıkarılmaya çalışılıyor...");
                    
                    // HTML içerisinden yorumları bulmaya çalış
                    var commentDataMatches = Regex.Matches(content, @"""text"":""([^""\\]*(?:\\.[^""\\]*)*)"",""created_at"":(\d+\.?\d*),""owner"":{""id"":""(\d+)"",""profile_pic_url"":""([^""]+)"",""username"":""([^""]+)""}");
                    
                    foreach (Match match in commentDataMatches)
                    {
                        try
                        {
                            var commentText = match.Groups[1].Value;
                            // Escape karakterlerini temizle (JSON string format)
                            commentText = Regex.Unescape(commentText);
                            
                            var createdAt = double.Parse(match.Groups[2].Value);
                            var ownerId = match.Groups[3].Value;
                            var ownerPic = match.Groups[4].Value;
                            var ownerName = match.Groups[5].Value;
                            
                            var comment = new Comment();
                            comment.Id = $"{ownerId}_{createdAt}";
                            comment.Text = commentText;
                            comment.Date = Tools.UnixTimeStampToDateTime(createdAt);
                            comment.OwnerName = ownerName;
                            comment.OwnerPicture = ownerPic;
                            comments.Add(comment);
                        }
                        catch { /* Hatalı yorumları atla */ }
                    }
                    
                    // Hiç yorum bulamazsak sistem mesajı ekle
                    if (comments.Count == 0)
                    {
                        var sysComment = new Comment();
                        sysComment.Id = "info_" + DateTime.Now.Ticks;
                        sysComment.Text = "Bu gönderiye ait yorum bulunamadı veya yorumlar kısıtlanmış durumda.";
                        sysComment.Date = DateTime.Now;
                        sysComment.OwnerName = "system_info";
                        sysComment.OwnerPicture = "https://instagram.com/favicon.ico";
                        comments.Add(sysComment);
                    }
                    
                    Console.WriteLine($"{comments.Count} yorum HTML içeriğinden çıkarıldı");
                    return comments;
                }
                
                // 4. Medya ID ile yorumları çekelim (modern API)
                var commentsUrl = $"https://www.instagram.com/api/v1/media/{mediaId}/comments/";
                var commentsClient = new RestClient(commentsUrl);
                var commentsRequest = new RestRequest(Method.GET);
                
                // Gerekli HTTP başlıkları ekle
                commentsRequest.AddHeader("User-Agent", GetUserAgent());
                commentsRequest.AddHeader("Accept", "*/*");
                commentsRequest.AddHeader("X-IG-App-ID", "936619743392459");
                commentsRequest.AddHeader("X-ASBD-ID", "129477");
                commentsRequest.AddHeader("X-IG-WWW-Claim", "0");
                
                // CSRF token ekle
                string csrfToken = GetCsrfToken(cookies);
                if (!string.IsNullOrEmpty(csrfToken))
                {
                    commentsRequest.AddHeader("X-CSRFToken", csrfToken);
                }
                
                // Parametreleri ekle
                commentsRequest.AddQueryParameter("can_support_threading", "true");
                commentsRequest.AddQueryParameter("permalink_enabled", "false");
                if (!string.IsNullOrEmpty(after))
                {
                    commentsRequest.AddQueryParameter("min_id", after);
                }
                
                // Çerezleri ekle
                foreach (var cookie in cookies)
                {
                    commentsRequest.AddCookie(cookie.Key, cookie.Value);
                }
                
                Console.WriteLine("Yorumlar API'si çağrılıyor...");
                IRestResponse commentsResponse = commentsClient.Execute(commentsRequest);
                Thread.Sleep(rnd.Next(2000, 4000));
                
                if (commentsResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Yorumlar API hatası: {commentsResponse.StatusCode}, Yanıt: {commentsResponse.Content}");
                    throw new Exception($"Yorumlar alınamadı: {commentsResponse.StatusCode}");
                }
                
                // 5. API yanıtını işle
                try
                {
                    var cleanedCommentsResponse = CleanInstagramResponse(commentsResponse.Content);
                    dynamic commentsData = JObject.Parse(cleanedCommentsResponse);
                    
                    // Hata kontrolü
                    if (commentsData.status != "ok")
                    {
                        throw new Exception("Instagram API yanıtı başarısız: " + commentsData.message);
                    }
                    
                    // Yorumları işle
                    if (commentsData.comments != null)
                    {
                        foreach (var commentItem in commentsData.comments)
                        {
                            try
                            {
                                var comment = new Comment();
                                comment.Id = commentItem.pk;
                                comment.Text = commentItem.text;
                                comment.Date = Tools.UnixTimeStampToDateTime((double)commentItem.created_at);
                                comment.OwnerName = commentItem.user.username;
                                comment.OwnerPicture = commentItem.user.profile_pic_url;
                                comments.Add(comment);
                            }
                            catch { /* Hatalı yorumu atla */ }
                        }
                    }
                    
                    // Hiç yorum bulamazsak
                    if (comments.Count == 0)
                    {
                        var sysComment = new Comment();
                        sysComment.Id = "info_" + DateTime.Now.Ticks;
                        sysComment.Text = "Bu gönderide henüz hiç yorum yok veya yorumlar gizlenmiş olabilir.";
                        sysComment.Date = DateTime.Now;
                        sysComment.OwnerName = "system_info";
                        sysComment.OwnerPicture = "https://instagram.com/favicon.ico";
                        comments.Add(sysComment);
                    }
                    
                    // 6. Sayfalama varsa devam
                    bool hasNextPage = commentsData.has_more_comments == true;
                    string nextCursor = commentsData.next_min_id?.ToString();
                    
                    if (hasNextPage && !string.IsNullOrEmpty(nextCursor) && pageCount > 1)
                    {
                        var nextComments = GetComments(shortcode, cookies, postPerPage: postPerPage, pageCount: --pageCount, after: nextCursor);
                        comments.AddRange(nextComments);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Yorumlar API yanıtı işlenirken hata: " + ex.Message);
                    
                    // API yanıtı işlenemezse, HTML'den çıkardığımız yorumları döndürelim
                    if (comments.Count == 0)
                    {
                        var sysComment = new Comment();
                        sysComment.Id = "error_" + DateTime.Now.Ticks;
                        sysComment.Text = "Yorumlar alınırken bir hata oluştu: " + ex.Message;
                        sysComment.Date = DateTime.Now;
                        sysComment.OwnerName = "system_error";
                        sysComment.OwnerPicture = "https://instagram.com/favicon.ico";
                        comments.Add(sysComment);
                    }
                }
                
                Console.WriteLine($"Toplam {comments.Count} yorum bulundu");
                return comments;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Yorumlar işlenirken hata: " + ex.Message);
                
                // Hata durumunda bilgilendirme yorumu ekle
                var errorComment = new Comment();
                errorComment.Id = "error_" + DateTime.Now.Ticks;
                errorComment.Text = "Yorumlar alınırken bir hata oluştu: " + ex.Message;
                errorComment.Date = DateTime.Now;
                errorComment.OwnerName = "system_error";
                errorComment.OwnerPicture = "https://instagram.com/favicon.ico";
                comments.Add(errorComment);
                
                return comments;
            }
        }
    }
}
