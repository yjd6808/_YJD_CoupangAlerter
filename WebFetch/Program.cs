/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 사용자가 9222 포트로 열어둔 엣지 브라우저에 CDP로 접속해
 * 새 탭에서 URL을 연 뒤 HTML을 표준출력으로 반환한다.
 * 
 * 사용법: WebFetch.exe <url>
 * 종료코드: 0 성공 / 1 실패 (실패 사유는 stderr)
 * * * * * * * * * * * * *
 */

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebFetch
{
    class Program
    {
        private const int CdpPort = 9222;
        private static string CdpEndpoint => $"http://127.0.0.1:{CdpPort}";

        static async Task<int> Main(string[] args)
        {
            // stdout/stderr 라이터가 생성되기 전에 UTF-8로 고정한다.
            // 안 그러면 stderr가 시스템 코드페이지(CP949)로 기록되고,
            // 앱이 UTF-8로 읽으면서 한글 에러 메시지가 깨진다.
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: WebFetch.exe <url>");
                return 1;
            }

            try
            {
                string html = await FetchAsync(args[0]);
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

        static async Task<string> FetchAsync(string url)
        {
            using var playwright = await Playwright.CreateAsync();

            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync(CdpEndpoint,
                    new BrowserTypeConnectOverCDPOptions { Timeout = 10000 });
            }
            catch (Exception)
            {
                throw new Exception("실행 중인 엣지 브라우저에 접속하지 못했습니다. 엣지를 --remote-debugging-port=9222 옵션으로 실행하세요.");
            }

            if (browser.Contexts.Count == 0)
                throw new Exception("엣지에 열린 브라우저 컨텍스트가 없습니다.");

            IBrowserContext context = browser.Contexts[0];
            var page = await context.NewPageAsync();

            try
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });

                return await page.ContentAsync();
            }
            finally
            {
                try { await page.CloseAsync(); } catch { }
            }
        }
    }
}
