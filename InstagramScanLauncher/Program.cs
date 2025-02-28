using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstagramAPI.Models;
using System.IO;
using Newtonsoft.Json;

namespace InstagramScanLauncher
{
    class Program
    {
        private static Dictionary<string, string> _cookies;
        private static InstagramAPI.Functions _instagramFunc;
        
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("========================================");
                Console.WriteLine("Instagram GraphQL Scanner - v1.0");
                Console.WriteLine("========================================");
                
                // Oturum başlatma
                InitializeSession();
                
                // Ana programı çalıştır
                RunMainMenu();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nUYGULAMA HATASI: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nUygulamadan çıkmak için bir tuşa basın...");
                Console.ReadKey();
            }
        }
        
        /// <summary>
        /// Instagram oturumu başlatır, tarayıcıyı açarak kullanıcıdan login olmayı ister
        /// </summary>
        static void InitializeSession()
        {
            Console.WriteLine("Instagram oturumu başlatılıyor...");
            Console.WriteLine("NOT: Tarayıcı açılarak Instagram hesabınıza giriş yapmanız istenecek.");
            Console.WriteLine("Giriş yaptıktan sonra tarayıcı otomatik olarak kapanacak.");
            
            // Cookie var mı, varsa yükle
            string cookiePath = "instagram_cookies.json";
            
            if (File.Exists(cookiePath))
            {
                try
                {
                    Console.WriteLine("Kaydedilmiş oturum bilgisi bulundu. Yüklenmeye çalışılıyor...");
                    string jsonCookies = File.ReadAllText(cookiePath);
                    _cookies = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonCookies);
                    
                    // Oturum bilgisi geçerli mi test et
                    Console.WriteLine("Oturum bilgisi test ediliyor...");
                    _instagramFunc = new InstagramAPI.Functions();
                    
                    // Test için istekle sayfa yükle
                    var testUrl = "https://www.instagram.com/";
                    var client = new RestSharp.RestClient(testUrl);
                    var request = new RestSharp.RestRequest(RestSharp.Method.GET);
                    
                    foreach (var cookie in _cookies)
                    {
                        request.AddCookie(cookie.Key, cookie.Value);
                    }
                    
                    var response = client.Execute(request);
                    
                    // Yeniden giriş ekranına yönlendiriyorsa oturum geçersizdir
                    if (response.Content.Contains("\"loginPage\"") || response.Content.Contains("/accounts/login/"))
                    {
                        Console.WriteLine("Kaydedilmiş oturum süresi dolmuş. Yeniden giriş yapmanız gerekiyor.");
                        _cookies = GetCookies();
                        SaveCookies(_cookies, cookiePath);
                    }
                    else
                    {
                        Console.WriteLine("Oturum bilgisi başarıyla yüklendi!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Kaydedilmiş oturum bilgisi yüklenemedi: {ex.Message}");
                    Console.WriteLine("Yeniden giriş yapılacak...");
                    _cookies = GetCookies();
                    SaveCookies(_cookies, cookiePath);
                }
            }
            else
            {
                _cookies = GetCookies();
                SaveCookies(_cookies, cookiePath);
            }
            
            _instagramFunc = new InstagramAPI.Functions();
            Console.WriteLine($"Oturum başarıyla açıldı. {_cookies.Count} cookie bulunuyor.");
        }
        
        /// <summary>
        /// Ana programı çalıştırır, kullanıcı arayüzünü gösterir
        /// </summary>
        static void RunMainMenu()
        {
            bool exit = false;
            
            while (!exit)
            {
                Console.WriteLine("\n========================================");
                Console.WriteLine("Instagram Scanner Menü");
                Console.WriteLine("========================================");
                Console.WriteLine("1. Kullanıcı bilgilerini göster");
                Console.WriteLine("2. Kullanıcı paylaşımlarını göster");
                Console.WriteLine("3. Hashtag ile paylaşımları ara");
                Console.WriteLine("4. Bir post'un yorumlarını göster");
                Console.WriteLine("0. Çıkış");
                Console.WriteLine("========================================");
                Console.Write("Seçiminiz: ");
                
                string choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        ShowUserInfo();
                        break;
                    case "2":
                        ShowUserPosts();
                        break;
                    case "3":
                        SearchHashtag();
                        break;
                    case "4":
                        ShowPostComments();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Geçersiz seçim. Tekrar deneyin.");
                        break;
                }
            }
            
            Console.WriteLine("Program sonlandırılıyor...");
        }
        
        /// <summary>
        /// Kullanıcı bilgilerini gösterir
        /// </summary>
        static void ShowUserInfo()
        {
            try
            {
                Console.Write("\nKullanıcı adı girin: ");
                string username = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    Console.WriteLine("Kullanıcı adı boş olamaz!");
                    return;
                }
                
                Console.WriteLine($"{username} kullanıcısı için bilgiler getiriliyor...");
                var user = _instagramFunc.GetUser(username, _cookies);
                
                Console.WriteLine("\n=== Kullanıcı Bilgileri ===");
                Console.WriteLine($"ID: {user.Id}");
                Console.WriteLine($"Kullanıcı Adı: {user.UserName}");
                Console.WriteLine($"Ad-Soyad: {user.FullName}");
                Console.WriteLine($"Biyo: {user.Biography}");
                Console.WriteLine($"Gönderi Sayısı: {user.PostCount}");
                Console.WriteLine($"Takipçi Sayısı: {user.FollowerCount}");
                Console.WriteLine($"Takip Edilen: {user.FollowCount}");
                Console.WriteLine($"Profil Fotoğrafı: {user.ProfilePictureHD}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Kullanıcının paylaşımlarını gösterir
        /// </summary>
        static void ShowUserPosts()
        {
            try
            {
                Console.Write("\nKullanıcı adı girin: ");
                string username = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    Console.WriteLine("Kullanıcı adı boş olamaz!");
                    return;
                }
                
                Console.WriteLine($"{username} kullanıcısı için bilgiler getiriliyor...");
                var user = _instagramFunc.GetUser(username, _cookies);
                
                Console.Write("Kaç sayfa gönderi istiyorsunuz? (Default: 1): ");
                if (!int.TryParse(Console.ReadLine(), out int pageCount) || pageCount < 1)
                {
                    pageCount = 1;
                }
                
                Console.WriteLine($"{username} kullanıcısının gönderileri getiriliyor...");
                var posts = _instagramFunc.GetPostsFromUserId(user.Id, _cookies, pageCount: pageCount);
                
                Console.WriteLine($"\n=== {username} kullanıcısının gönderileri ({posts.Count} post) ===");
                
                int index = 1;
                foreach (var post in posts)
                {
                    Console.WriteLine($"\n{index}. Post: {post.PostUrl}");
                    Console.WriteLine($"Açıklama: {(post.Caption.Length > 50 ? post.Caption.Substring(0, 50) + "..." : post.Caption)}");
                    Console.WriteLine($"Beğeni: {post.LikeCount}, Yorum: {post.CommentCount}");
                    Console.WriteLine($"Tarih: {post.Date}");
                    index++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Hashtag ile ilgili postları gösterir
        /// </summary>
        static void SearchHashtag()
        {
            try
            {
                Console.Write("\nHashtag girin (#işareti olmadan): ");
                string tag = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(tag))
                {
                    Console.WriteLine("Hashtag boş olamaz!");
                    return;
                }
                
                Console.Write("Kaç adet post görmek istiyorsunuz? (1-50): ");
                if (!int.TryParse(Console.ReadLine(), out int postCount) || postCount < 1 || postCount > 50)
                {
                    postCount = 20;
                }
                
                Console.WriteLine($"#{tag} hashtagi ile ilgili postlar getiriliyor...");
                var posts = _instagramFunc.GetPostsFromTag(tag, _cookies, postPerPage: postCount);
                
                Console.WriteLine($"\n=== #{tag} hashtagi ile ilgili postlar ({posts.Count} post) ===");
                
                int index = 1;
                foreach (var post in posts)
                {
                    Console.WriteLine($"\n{index}. Post: {post.PostUrl}");
                    Console.WriteLine($"Açıklama: {(post.Caption.Length > 50 ? post.Caption.Substring(0, 50) + "..." : post.Caption)}");
                    Console.WriteLine($"Beğeni: {post.LikeCount}, Yorum: {post.CommentCount}");
                    Console.WriteLine($"Tarih: {post.Date}");
                    index++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Bir gönderinin yorumlarını gösterir
        /// </summary>
        static void ShowPostComments()
        {
            try
            {
                Console.Write("\nPost URL veya kısa kodu girin: ");
                string input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("URL veya kısa kod boş olamaz!");
                    return;
                }
                
                // URL'den kısa kodu çıkar
                string shortcode = input;
                
                if (input.Contains("instagram.com/p/"))
                {
                    var parts = input.Split(new[] { "/p/" }, StringSplitOptions.None);
                    if (parts.Length > 1)
                    {
                        shortcode = parts[1].Replace("/", "").Trim();
                    }
                }
                
                Console.WriteLine($"Post yorumları getiriliyor: {shortcode}");
                var comments = _instagramFunc.GetComments(shortcode, _cookies);
                
                Console.WriteLine($"\n=== Post Yorumları ({comments.Count} yorum) ===");
                
                int index = 1;
                foreach (var comment in comments)
                {
                    Console.WriteLine($"\n{index}. {comment.OwnerName} diyor ki:");
                    Console.WriteLine($"\"{comment.Text}\"");
                    Console.WriteLine($"Tarih: {comment.Date}");
                    index++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Hata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Oturum çerezlerini kaydeder
        /// </summary>
        static void SaveCookies(Dictionary<string, string> cookies, string path)
        {
            try
            {
                string jsonCookies = JsonConvert.SerializeObject(cookies);
                File.WriteAllText(path, jsonCookies);
                Console.WriteLine("Oturum bilgileri kaydedildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Oturum bilgileri kaydedilemedi: {ex.Message}");
            }
        }

        /// <summary>
        /// Selenium ile tarayıcıyı açarak instagram login istenir ve login sonrası tarayıcıyı kapatarak aktif cookie verilerini döner.
        /// </summary>
        static Dictionary<string, string> GetCookies()
        {
            /* 
             * Bazı fonksiyonları kullanmak için cookie istenecek. 
             * Aşağıdaki yöntem ile cookie verisi alınıp fonksiyona gönderilebilir.
             */
            Console.WriteLine("Chrome tarayıcısı başlatılıyor...");
            
            var options = new ChromeOptions();
            // Daha iyi görünüm için pencere boyutunu ayarla
            options.AddArgument("--window-size=1280,800");
            
            Console.WriteLine("Instagram giriş sayfası açılıyor. Lütfen hesabınıza giriş yapın.");
            ChromeDriver driver = new ChromeDriver(options);
            
            Thread.Sleep(1000);
            driver.Navigate().GoToUrl("https://www.instagram.com/accounts/login/");
            
            Console.WriteLine("Giriş yapmanız bekleniyor...");
            while (true)
            {
                if (!driver.Url.Contains("accounts/login"))
                {
                    Console.WriteLine("Giriş başarılı! Tarayıcı 3 saniye içinde kapanacak.");
                    break;
                }
                Thread.Sleep(1000);
            }
            
            // Cookielerin tam yüklenmesi için biraz bekle
            Thread.Sleep(3000);
            
            var driverCookies = driver.Manage().Cookies;
            var cookies = driverCookies.AllCookies.ToDictionary(w => w.Name, x => x.Value);
            driver.Close();
            driver.Quit();
            return cookies;
        }
    }
}
