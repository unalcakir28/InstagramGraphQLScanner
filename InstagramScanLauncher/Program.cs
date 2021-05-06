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
            InstagramAPI.Functions instagramFunc = new InstagramAPI.Functions();
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

            var posts = instagramFunc.GetPostsFromTag("test",2);
        }
    }
}
