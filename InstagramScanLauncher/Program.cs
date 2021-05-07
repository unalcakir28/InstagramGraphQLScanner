using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InstagramScanLauncher
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cookies = getCookies();
            InstagramAPI.Functions instagramFunc = new InstagramAPI.Functions();
            var user = instagramFunc.GetUser("setup34digital", cookies);
            var userPosts = instagramFunc.GetPostsFromUserId(user.Id, cookies, postPerPage: 20, pageCount: 2);
            var postComments = instagramFunc.GetComments(userPosts[0].ShortCode, cookies, postPerPage: 20, pageCount: 3);
            var tagPosts = instagramFunc.GetPostsFromTag("pazar", cookies, postPerPage: 10, pageCount: 3);
        }


        /// <summary>
        /// Selenium ile tarayıcıyı açarak instagram login istenir ve login sonrası tarayıcıyı kapatarak aktif cookie verilerini döner.
        /// </summary>
        static Dictionary<string, string> getCookies()
        {
            /* 
             * Bazı fonksiyonları kullanmak için cookie istenecek. 
             * Aşağıdaki yöntem ile cookie verisi alınıp fonksiyona gönderilebilir.
             */

            ChromeDriver driver = new ChromeDriver();
            Thread.Sleep(1000);
            driver.Navigate().GoToUrl("https://www.instagram.com/accounts/login/");

            while (true)
            {
                if (!driver.Url.Contains("accounts/login"))
                    break;
                Thread.Sleep(1000);
            }

            var driverCookies = driver.Manage().Cookies;
            var cookies = driverCookies.AllCookies.ToDictionary(w => w.Name, x => x.Value);
            driver.Close();
            return cookies;
        }
    }
}
