/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 사용자가 9222 포트로 열어둔 엣지 브라우저에 CDP로 접속해
 * URL을 연 뒤 HTML을 표준출력으로 반환한다.
 * 
 * 사용법: WebFetch.exe <url> [tab|window|hidden]
 *   tab    : 기존 엣지 창에 새 탭으로 연다 (기본값)
 *   window : 새 엣지 창으로 연다
 *   hidden : 새 엣지 창을 최소화 상태로 열어 화면에 표시하지 않는다
 * 종료코드: 0 성공 / 1 실패 (실패 사유는 stderr)
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
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
                Console.Error.WriteLine("Usage: WebFetch.exe <url> [tab|window|hidden]");
                return 1;
            }

            string mode = args.Length >= 2 ? args[1].Trim().ToLowerInvariant() : "tab";

            if (mode != "tab" && mode != "window" && mode != "hidden")
            {
                Console.Error.WriteLine($"알 수 없는 창 모드입니다: {mode} (tab|window|hidden)");
                return 1;
            }

            try
            {
                string html = await FetchAsync(args[0], mode);
                Console.Write(html);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        static async Task<string> FetchAsync(string url, string mode)
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
            IPage page = mode == "tab"
                ? await context.NewPageAsync()
                : await CreateWindowPageAsync(browser, context, mode);

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

        // CDP Target.createTarget로 새 엣지 창을 생성한다.
        // hidden 모드는 창을 최소화 상태로 생성해 화면에 표시하지 않는다.
        // 마지막 탭이 닫히면 창도 함께 닫힌다.
        //
        // 동시에 여러 WebFetch가 실행되어도 서로의 창을 잘못 잡지 않도록
        // 고유 fragment가 붙은 about:blank로 창을 만들고 그 URL로 자신의 페이지를 식별한다.
        static async Task<IPage> CreateWindowPageAsync(IBrowser browser, IBrowserContext context, string mode)
        {
            var existingPages = new HashSet<IPage>(context.Pages);
            string placeholderUrl = $"about:blank#{Guid.NewGuid():N}";

            var cdp = await browser.NewBrowserCDPSessionAsync();
            JsonElement? response = await cdp.SendAsync("Target.createTarget", new Dictionary<string, object>
            {
                ["url"] = placeholderUrl,
                ["newWindow"] = true,
                ["background"] = true,
                ["windowState"] = mode == "hidden" ? "minimized" : "normal"
            });

            // hidden 모드 보강: 창 최소화를 명시적으로 한 번 더 설정한다.
            // (createTarget의 windowState를 무시하는 환경 대비, 실패해도 무시)
            if (mode == "hidden" && response.HasValue && response.Value.TryGetProperty("targetId", out JsonElement targetIdProp))
            {
                try
                {
                    string targetId = targetIdProp.GetString() ?? string.Empty;
                    JsonElement? windowResponse = await cdp.SendAsync("Browser.getWindowForTarget", new Dictionary<string, object>
                    {
                        ["targetId"] = targetId
                    });

                    if (windowResponse.HasValue && windowResponse.Value.TryGetProperty("windowId", out JsonElement windowIdProp))
                    {
                        await cdp.SendAsync("Browser.setWindowBounds", new Dictionary<string, object>
                        {
                            ["windowId"] = windowIdProp.GetInt32(),
                            ["bounds"] = new Dictionary<string, object> { ["windowState"] = "minimized" }
                        });
                    }
                }
                catch { }
            }

            // 새로 생성된 창의 페이지가 Playwright 컨텍스트에 나타날 때까지 대기한다.
            for (int i = 0; i < 100; i++)
            {
                foreach (var candidate in context.Pages)
                {
                    if (!existingPages.Contains(candidate) && candidate.Url == placeholderUrl)
                        return candidate;
                }

                await Task.Delay(100);
            }

            throw new Exception("새 엣지 창을 열지 못했습니다.");
        }
    }
}
