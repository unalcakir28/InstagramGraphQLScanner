using Microsoft.Extensions.Configuration;
using OpenQA.Selenium.Chrome;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InstagramAPI.Logging;
using InstagramAPI.Configuration;
using InstagramAPI.Models;

namespace InstagramScanLauncher
{
    class Program
    {
        private static Dictionary<string, string> _cookies;
        private static InstagramAPI.Functions _instagramFunc;
        private static readonly string LogFolderPath = "logs";
        private static readonly string CookiePath = "instagram_cookies.json";
        private static InstagramAPI.Logging.ILogger _logger;
        private static IConfiguration _configuration;

        static async Task Main(string[] args)
        {
            try
            {
                // Yapılandırma ve loglama ayarlarını yükle
                InitializeConfiguration();
                InitializeLogging();

                _logger.LogInfo("========================================");
                _logger.LogInfo("Instagram GraphQL Scanner - v1.0 Başlatıldı");
                _logger.LogInfo("========================================");
                
                Console.WriteLine("========================================");
                Console.WriteLine("Instagram GraphQL Scanner - v1.0");
                Console.WriteLine("========================================");
                
                // Oturum başlatma
                InitializeSession();
                
                // Ana programı çalıştır
                await RunMainMenu();
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.LogError("Program Hatası", ex);
                else
                    Console.Error.WriteLine($"Kritik hata: {ex.Message}\n{ex.StackTrace}");
                    
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nUYGULAMA HATASI: {ex.Message}");
                Console.WriteLine("Detaylı hata bilgisi log dosyasına kaydedildi.");
                Console.ResetColor();
                Console.WriteLine("\nUygulamadan çıkmak için bir tuşa basın...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Yapılandırma dosyasını yükler
        /// </summary>
        private static void InitializeConfiguration()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }

        /// <summary>
        /// Serilog yapılandırmasını yükler ve loglama altyapısını başlatır
        /// </summary>
        private static void InitializeLogging()
        {
            // Logs klasörünü oluştur
            if (!Directory.Exists(LogFolderPath))
                Directory.CreateDirectory(LogFolderPath);

            // Serilog yapılandırması
            LoggingConfig.ConfigureLogging();
            _logger = LoggingConfig.CreateLogger();
            
            // Kritik hatalar için global hata yakalama
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => 
            {
                _logger?.LogError("Kritik Hata", e.ExceptionObject as Exception);
            };
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
            if (File.Exists(CookiePath))
            {
                try
                {
                    Console.WriteLine("Kaydedilmiş oturum bilgisi bulundu. Yüklenmeye çalışılıyor...");
                    string jsonCookies = File.ReadAllText(CookiePath);
                    _cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonCookies);
                    
                    // Oturum bilgisi geçerli mi test et
                    Console.WriteLine("Oturum bilgisi test ediliyor...");
                    
                    // API yapılandırmasını yükle
                    var apiConfig = InstagramApiConfig.LoadFromConfiguration();
                    _instagramFunc = new InstagramAPI.Functions(_logger, apiConfig);
                    
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
                        SaveCookies(_cookies);
                    }
                    else
                    {
                        Console.WriteLine("Oturum bilgisi başarıyla yüklendi!");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Kaydedilmiş oturum bilgisi yüklenemedi: {ex.Message}", ex);
                    Console.WriteLine($"Kaydedilmiş oturum bilgisi yüklenemedi: {ex.Message}");
                    Console.WriteLine("Yeniden giriş yapılacak...");
                    _cookies = GetCookies();
                    SaveCookies(_cookies);
                }
            }
            else
            {
                _cookies = GetCookies();
                SaveCookies(_cookies);
            }
            
            // API yapılandırmasını yükle
            var config = InstagramApiConfig.LoadFromConfiguration();
            _instagramFunc = new InstagramAPI.Functions(_logger, config);
            
            _logger.LogInfo($"Oturum başarıyla açıldı. {_cookies.Count} cookie bulunuyor.");
            Console.WriteLine($"Oturum başarıyla açıldı. {_cookies.Count} cookie bulunuyor.");
        }
        
        /// <summary>
        /// Ana menüyü çalıştırır ve kullanıcı arayüzünü gösterir
        /// </summary>
        static async Task RunMainMenu()
        {
            bool exit = false;
            
            while (!exit)
            {
                try
                {
                    Console.Clear(); // Ekranı temizle
                    Console.WriteLine("\n========================================");
                    Console.WriteLine("Instagram Scanner Menü");
                    Console.WriteLine("========================================");
                    Console.WriteLine("1. Kullanıcı bilgilerini göster");
                    Console.WriteLine("2. Kullanıcı paylaşımlarını göster");
                    Console.WriteLine("3. Hashtag ile paylaşımları ara");
                    Console.WriteLine("4. Bir post'un yorumlarını göster");
                    Console.WriteLine("5. Log dosyasını görüntüle");
                    Console.WriteLine("0. Çıkış");
                    Console.WriteLine("========================================");
                    Console.Write("Seçiminiz: ");
                    
                    string choice = Console.ReadLine();
                    _logger.LogInfo($"Menü seçimi: {choice}");
                    
                    switch (choice)
                    {
                        case "1":
                            await ShowUserInfo();
                            break;
                        case "2":
                            await ShowUserPosts();
                            break;
                        case "3":
                            await SearchHashtag();
                            break;
                        case "4":
                            await ShowPostComments();
                            break;
                        case "5":
                            ShowLogFile();
                            break;
                        case "0":
                            exit = true;
                            _logger.LogInfo("Program sonlandırılıyor...");
                            break;
                        default:
                            Console.WriteLine("\nGeçersiz seçim. Tekrar deneyin.");
                            Thread.Sleep(1000);
                            break;
                    }
                    
                    if (!exit)
                    {
                        Console.WriteLine("\nAna menüye dönmek için bir tuşa basın...");
                        Console.ReadKey();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Menü İşlemi", ex);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nHata oluştu: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine("Ana menüye dönmek için bir tuşa basın...");
                    Console.ReadKey();
                }
            }
        }
        
        /// <summary>
        /// Log dosyasının içeriğini gösterir
        /// </summary>
        static void ShowLogFile()
        {
            try
            {
                var logFiles = Directory.GetFiles(LogFolderPath, "*.log");
                
                if (logFiles.Length > 0)
                {
                    // En son güncellenen log dosyasını al
                    var latestLogFile = logFiles
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .First();
                    
                    Console.Clear();
                    Console.WriteLine($"\n=== Log Dosyası İçeriği ({Path.GetFileName(latestLogFile)}) ===\n");
                    
                    string[] logs = File.ReadAllLines(latestLogFile);
                    
                    // Son 50 log satırını göster
                    foreach (var log in logs.Skip(Math.Max(0, logs.Length - 50)))
                    {
                        if (log.Contains("ERROR") || log.Contains("[Error]"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(log);
                            Console.ResetColor();
                        }
                        else if (log.Contains("WARNING") || log.Contains("[Warning]"))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine(log);
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine(log);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Log dosyası henüz oluşturulmamış.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Log Görüntüleme", ex);
                Console.WriteLine($"Log dosyası okunamadı: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Kullanıcı bilgilerini gösterir
        /// </summary>
        static async Task ShowUserInfo()
        {
            try
            {
                Console.Write("\nKullanıcı adı girin: ");
                string username = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.LogInfo("Boş kullanıcı adı girişi");
                    Console.WriteLine("Kullanıcı adı boş olamaz!");
                    return;
                }
                
                _logger.LogInfo($"Kullanıcı bilgileri isteniyor: {username}");
                Console.WriteLine($"\n{username} kullanıcısı için bilgiler getiriliyor...");
                
                var user = await _instagramFunc.GetUserAsync(username, _cookies);
                
                if (user != null)
                {
                    _logger.LogInfo($"Kullanıcı bilgileri başarıyla alındı: {username}");
                    
                    Console.WriteLine("\n=== Kullanıcı Bilgileri ===");
                    Console.WriteLine($"ID: {user.Id}");
                    Console.WriteLine($"Kullanıcı Adı: {user.UserName}");
                    Console.WriteLine($"Ad-Soyad: {user.FullName}");
                    Console.WriteLine($"Biyo: {user.Biography}");
                    Console.WriteLine($"Gönderi Sayısı: {user.PostCount:N0}");
                    Console.WriteLine($"Takipçi Sayısı: {user.FollowerCount:N0}");
                    Console.WriteLine($"Takip Edilen: {user.FollowCount:N0}");
                    Console.WriteLine($"Profil Fotoğrafı: {user.ProfilePictureHD}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Kullanıcı Bilgileri", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nHata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Kullanıcının paylaşımlarını gösterir
        /// </summary>
        static async Task ShowUserPosts()
        {
            try
            {
                Console.Write("\nKullanıcı adı girin: ");
                string username = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    _logger.LogInfo("Boş kullanıcı adı girişi");
                    Console.WriteLine("Kullanıcı adı boş olamaz!");
                    return;
                }
                
                _logger.LogInfo($"Kullanıcı postları isteniyor: {username}");
                Console.WriteLine($"\n{username} kullanıcısı için bilgiler getiriliyor...");
                
                var user = await _instagramFunc.GetUserAsync(username, _cookies);
                
                Console.Write("Kaç sayfa gönderi istiyorsunuz? (Default: 1): ");
                if (!int.TryParse(Console.ReadLine(), out int pageCount) || pageCount < 1)
                {
                    pageCount = 1;
                }
                
                _logger.LogInfo($"{username} kullanıcısı için {pageCount} sayfa post isteniyor");
                Console.WriteLine($"\n{username} kullanıcısının gönderileri getiriliyor...");
                
                var posts = await _instagramFunc.GetPostsFromUserIdAsync(user.Id, _cookies, pageCount: pageCount);
                
                if (posts != null && posts.Any())
                {
                    _logger.LogInfo($"{posts.Count} post başarıyla alındı");
                    
                    Console.WriteLine($"\n=== {username} kullanıcısının gönderileri ({posts.Count:N0} post) ===");
                    
                    int index = 1;
                    foreach (var post in posts)
                    {
                        Console.WriteLine($"\n{index}. Post: {post.PostUrl}");
                        Console.WriteLine($"Açıklama: {(post.Caption?.Length > 50 ? post.Caption.Substring(0, 50) + "..." : post.Caption)}");
                        Console.WriteLine($"Beğeni: {post.LikeCount:N0}, Yorum: {post.CommentCount:N0}");
                        Console.WriteLine($"Tarih: {post.Date:g}");
                        index++;
                    }
                }
                else
                {
                    _logger.LogInfo($"{username} kullanıcısının postu bulunamadı");
                    Console.WriteLine("Bu kullanıcının hiç gönderisi yok veya gönderileri gizli.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Kullanıcı Postları", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nHata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Hashtag ile ilgili postları gösterir
        /// </summary>
        static async Task SearchHashtag()
        {
            try
            {
                Console.Write("\nHashtag girin (#işareti olmadan): ");
                string tag = Console.ReadLine()?.Trim().ToLower();
                
                if (string.IsNullOrWhiteSpace(tag))
                {
                    _logger.LogInfo("Boş hashtag girişi");
                    Console.WriteLine("Hashtag boş olamaz!");
                    return;
                }
                
                Console.Write("Kaç adet post görmek istiyorsunuz? (1-50): ");
                if (!int.TryParse(Console.ReadLine(), out int postCount) || postCount < 1 || postCount > 50)
                {
                    postCount = 20;
                }
                
                _logger.LogInfo($"#{tag} hashtagi için {postCount} post isteniyor");
                Console.WriteLine($"\n#{tag} hashtagi ile ilgili postlar getiriliyor...");
                
                var posts = await _instagramFunc.GetPostsFromTagAsync(tag, _cookies, postPerPage: postCount);
                
                if (posts != null && posts.Any())
                {
                    _logger.LogInfo($"#{tag} için {posts.Count} post başarıyla alındı");
                    
                    Console.WriteLine($"\n=== #{tag} hashtagi ile ilgili postlar ({posts.Count:N0} post) ===");
                    
                    int index = 1;
                    foreach (var post in posts)
                    {
                        Console.WriteLine($"\n{index}. Post: {post.PostUrl}");
                        Console.WriteLine($"Açıklama: {(post.Caption?.Length > 50 ? post.Caption.Substring(0, 50) + "..." : post.Caption)}");
                        Console.WriteLine($"Beğeni: {post.LikeCount:N0}, Yorum: {post.CommentCount:N0}");
                        Console.WriteLine($"Tarih: {post.Date:g}");
                        index++;
                    }
                }
                else
                {
                    _logger.LogInfo($"#{tag} için post bulunamadı");
                    Console.WriteLine("Bu hashtag ile ilgili post bulunamadı veya Instagram erişimi kısıtlamış olabilir.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Hashtag Arama", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nHata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Bir gönderinin yorumlarını gösterir
        /// </summary>
        static async Task ShowPostComments()
        {
            try
            {
                Console.Write("\nPost URL veya kısa kodu girin: ");
                string input = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    _logger.LogInfo("Boş post URL/kodu girişi");
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
                
                _logger.LogInfo($"Post yorumları isteniyor: {shortcode}");
                Console.WriteLine($"\nPost yorumları getiriliyor: {shortcode}");
                
                var comments = await _instagramFunc.GetCommentsAsync(shortcode, _cookies);
                
                if (comments != null && comments.Any())
                {
                    _logger.LogInfo($"{comments.Count} yorum başarıyla alındı");
                    
                    Console.WriteLine($"\n=== Post Yorumları ({comments.Count:N0} yorum) ===");
                    
                    int index = 1;
                    foreach (var comment in comments)
                    {
                        if (comment.OwnerName.StartsWith("system_"))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                        }
                        
                        Console.WriteLine($"\n{index}. {comment.OwnerName} diyor ki:");
                        Console.WriteLine($"\"{comment.Text}\"");
                        Console.WriteLine($"Tarih: {comment.Date:g}");
                        
                        Console.ResetColor();
                        index++;
                    }
                }
                else
                {
                    _logger.LogInfo("Post için yorum bulunamadı");
                    Console.WriteLine("Bu postta hiç yorum yok veya yorumlar gizlenmiş.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Post Yorumları", ex);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nHata: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        /// <summary>
        /// Oturum çerezlerini kaydeder
        /// </summary>
        static void SaveCookies(Dictionary<string, string> cookies)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                string jsonCookies = JsonSerializer.Serialize(cookies, options);
                File.WriteAllText(CookiePath, jsonCookies);
                Console.WriteLine("Oturum bilgileri kaydedildi.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Oturum kaydedilirken hata", ex);
                Console.WriteLine($"Oturum bilgileri kaydedilemedi: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Selenium ile tarayıcıyı açarak instagram login istenir ve login sonrası tarayıcıyı kapatarak aktif cookie verilerini döner.
        /// </summary>
        static Dictionary<string, string> GetCookies()
        {
            _logger.LogInfo("Tarayıcı açılarak Instagram hesabı girişi isteniyor");
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
                    _logger.LogInfo("Instagram hesabına giriş başarılı");
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
            
            _logger.LogInfo($"{cookies.Count} adet cookie alındı");
            return cookies;
        }
    }
}
