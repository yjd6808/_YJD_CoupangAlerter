/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebFetch
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("URL argument required");
                return 1;
            }

            try
            {
                string html = await DownloadHtmlAsync(args[0]);
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write(html);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        static async Task<string> DownloadHtmlAsync(string url)
        {
            string installFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".playwright-installed");
            if (!File.Exists(installFlag))
            {
                int exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                if (exitCode != 0)
                    throw new Exception($"Playwright Chromium 설치 실패 (exit code: {exitCode})");
                File.WriteAllText(installFlag, "installed");
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });
            return await page.ContentAsync();
        }
    }
}
