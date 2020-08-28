using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.Events;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;

namespace AutoSignInvoker
{
    class Program
    {
        static Program()
        {
            Configuration = new ConfigurationBuilder()
                .AddUserSecrets<Program>(true)
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            _client = new HttpClient();
        }
        private static IConfiguration Configuration { get; }
        private static HttpClient _client;
        static async Task Main(string[] args)
        {
            var browser = new ChromeDriver();
            var waiter = new WebDriverWait(browser, TimeSpan.FromSeconds(10));

            // access sign page
            var url = Configuration["SignUrl"];

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                InteractExit("无效配置：SignUrl 应该是一个绝对Uri");
            }

            browser.Url = url;

            // wait redirection completed and page fully rendered
            var loginFrame = waiter.Until(driver => driver.FindElement(By.Id("loginFrame")));

            // rqeuired sign in
            if (loginFrame != null)
            {
                browser.SwitchTo().Frame("loginFrame");

                var userNameInput = browser.FindElementById("u");
                var passwordInput = browser.FindElementById("p");
                var loginButton = browser.FindElementById("go");

                var username = Configuration["QQ"];
                var password = Configuration["Password"];

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    InteractExit("无效的QQ或密码");
                }

                userNameInput.SendKeys(username);
                passwordInput.SendKeys(password);

                loginButton.Click();

                // wait 2s for rendering verification code
                var verificationCode = waiter.Until(driver => driver.FindElement(By.Id("tcaptcha_iframe")));
                if (verificationCode != null)
                {
                    browser.SwitchTo().Frame("tcaptcha_iframe");

                    var pendingImg = waiter.Until(driver => driver.FindElement(By.Id("slideBg")));

                    var pendingImgSrc = pendingImg.GetAttribute("src");

                    var uriBuilder = new UriBuilder(pendingImgSrc);

                    uriBuilder.Query = uriBuilder.Query.Replace("img_index=1", "img_index=0");

                    var completeImgSrc = uriBuilder.Uri.AbsoluteUri;

                    var slideBlock = browser.FindElementById("slideBlock");

                    await SlideVerificationCode(pendingImgSrc, completeImgSrc, pendingImg.Size.Width, browser, slideBlock);
                }
            }


            Console.WriteLine("Hello World!");
        }


        private static void InteractExit(string error)
        {
            Console.WriteLine(error);
            Console.WriteLine("Press Any key to exit!");
            Console.ReadLine();
            Environment.Exit(-1);
        }

        private static async Task SlideVerificationCode(string pending, string completed, int pendingImgWidth, IWebDriver driver, IWebElement slideBlock)
        {
            var penddingPictureTask = _client.GetStreamAsync(pending);

            var completePictureTask = _client.GetStreamAsync(completed);

            var pendingMap = new Bitmap(await penddingPictureTask);
            var completeMap = new Bitmap(await completePictureTask);

            var left = GetArgb(completeMap, pendingMap);

            var leftShift = (int)(left * ((double)pendingImgWidth / (double)pendingImgWidth) - 18);

            var actions = new Actions(driver);

            actions.DragAndDropToOffset(slideBlock, leftShift, 0).Build().Perform();
        }

        private static int GetArgb(Bitmap oldMap, Bitmap newMap)
        {
            for (int i = 0; i < newMap.Width; i++)
            {
                for (int j = 0; j < newMap.Height; j++)
                {
                    if (i >= 0 && i <= 1 && ((j >= 0 && j <= 1) || (j >= (newMap.Height - 2) && j <= (oldMap.Height - 1))))
                    {
                        continue;
                    }

                    if ((i >= (newMap.Width - 2) && i <= (newMap.Width - 1)) && ((j >= 0 && j <= 1) || (j >= (newMap.Height - 2) && j <= (newMap.Height - 1))))
                    {
                        continue;
                    }

                    var oldColor = oldMap.GetPixel(i, j);
                    var newColor = newMap.GetPixel(i, j);

                    if (Math.Abs(oldColor.R - newColor.R) > 60 || Math.Abs(oldColor.G - newColor.G) > 60 || Math.Abs(oldColor.B - newColor.B) > 60)
                    {
                        return i;
                    }
                }

            }
            // fallback
            return 0;
        }
    }
}
