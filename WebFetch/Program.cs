/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 사용자가 --remote-debugging-port 옵션으로 열어둔 엣지/크롬 브라우저에 CDP로 접속해
 * URL을 연 뒤 HTML을 표준출력으로 반환한다.
 * 
 * 사용법: WebFetch.exe <url> [tab|window|hidden|headless] [port]
 *   tab      : 기존 브라우저 창에 새 탭으로 연다 (기본값)
 *   window   : 새 브라우저 창으로 연다
 *   hidden   : 새 브라우저 창을 최소화 상태로 열어 화면에 표시하지 않는다
 *   headless : 라이브 브라우저의 쿠키만 복사해 별도 헤드리스 브라우저로 연다 (창 없음)
 *   port     : CDP 포트 (기본값 9222)
 * 종료코드: 0 성공 / 1 실패 (실패 사유는 stderr)
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebFetch
{
    class Program
    {
        private const int DefaultCdpPort = 9222;

        static async Task<int> Main(string[] args)
        {
            // stdout/stderr 라이터가 생성되기 전에 UTF-8로 고정한다.
            // 안 그러면 stderr가 시스템 코드페이지(CP949)로 기록되고,
            // 앱이 UTF-8로 읽으면서 한글 에러 메시지가 깨진다.
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("Usage: WebFetch.exe <url> [tab|window|hidden|headless] [port]");
                return 1;
            }

            string mode = args.Length >= 2 ? args[1].Trim().ToLowerInvariant() : "tab";

            if (mode != "tab" && mode != "window" && mode != "hidden" && mode != "headless")
            {
                Console.Error.WriteLine($"알 수 없는 창 모드입니다: {mode} (tab|window|hidden|headless)");
                return 1;
            }

            int port = DefaultCdpPort;
            if (args.Length >= 3 && (!int.TryParse(args[2].Trim(), out port) || port < 1 || port > 65535))
            {
                Console.Error.WriteLine($"알 수 없는 CDP 포트입니다: {args[2]} (1~65535)");
                return 1;
            }

            try
            {
                string html = await FetchAsync(args[0], mode, port);
                Console.Write(html);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        static async Task<string> FetchAsync(string url, string mode, int port)
        {
            using var playwright = await Playwright.CreateAsync();

            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{port}",
                    new BrowserTypeConnectOverCDPOptions { Timeout = 10000 });
            }
            catch (Exception)
            {
                throw new Exception($"CDP 포트 {port}에 접속하지 못했습니다. 엣지/크롬을 --remote-debugging-port={port} --user-data-dir=<전용폴더> 옵션으로 실행하세요.");
            }

            if (browser.Contexts.Count == 0)
                throw new Exception("브라우저에 열린 컨텍스트가 없습니다.");

            IBrowserContext context = browser.Contexts[0];

            // headless 모드는 라이브 브라우저에 페이지/창을 전혀 만들지 않는다.
            // 쿠키와 User-Agent만 읽어 별도 헤드리스 브라우저에서 페이지를 연다.
            if (mode == "headless")
                return await FetchHeadlessAsync(playwright, browser, context, url);

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

        // 라이브 브라우저에는 페이지/창을 만들지 않고 쿠키와 User-Agent만 읽는다.
        // 같은 브라우저 실행 파일(엣지/크롬)을 헤드리스로 새로 띄워 쿠키를 이식하므로
        // 라이브 브라우저의 창, 탭, 포커스, 작업표시줄에 변화가 생기지 않는다.
        static async Task<string> FetchHeadlessAsync(IPlaywright playwright, IBrowser liveBrowser, IBrowserContext liveContext, string url)
        {
            IReadOnlyList<BrowserContextCookiesResult> cookieResults = await liveContext.CookiesAsync();
            List<Cookie> cookies = new List<Cookie>(cookieResults.Count);

            foreach (BrowserContextCookiesResult result in cookieResults)
            {
                cookies.Add(new Cookie
                {
                    Name = result.Name,
                    Value = result.Value,
                    Domain = result.Domain,
                    Path = result.Path,
                    Expires = result.Expires,
                    HttpOnly = result.HttpOnly,
                    Secure = result.Secure,
                    SameSite = result.SameSite,
                    PartitionKey = result.PartitionKey
                });
            }

            string product = "";
            string userAgent = "";

            try
            {
                var cdp = await liveBrowser.NewBrowserCDPSessionAsync();
                JsonElement? versionResponse = await cdp.SendAsync("Browser.getVersion");

                if (versionResponse.HasValue)
                {
                    if (versionResponse.Value.TryGetProperty("product", out JsonElement productProp))
                        product = productProp.GetString() ?? "";
                    if (versionResponse.Value.TryGetProperty("userAgent", out JsonElement userAgentProp))
                        userAgent = userAgentProp.GetString() ?? "";
                }
            }
            catch
            {
                // 버전 조회에 실패해도 기본 경로 탐색으로 진행한다.
            }

            string? executablePath = FindBrowserExecutable(product);
            IBrowser headlessBrowser;

            try
            {
                headlessBrowser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    ExecutablePath = executablePath,
                    Args = new[]
                    {
                        "--disable-blink-features=AutomationControlled",
                        "--no-first-run",
                        "--no-default-browser-check"
                    }
                });
            }
            catch (Exception e)
            {
                string target = executablePath ?? "Playwright 번들 Chromium";
                throw new Exception($"헤드리스 브라우저를 실행하지 못했습니다: {target} ({e.Message})");
            }

            try
            {
                BrowserNewContextOptions contextOptions = new BrowserNewContextOptions
                {
                    Locale = "ko-KR",
                    TimezoneId = "Asia/Seoul"
                };

                if (!string.IsNullOrWhiteSpace(userAgent))
                    contextOptions.UserAgent = userAgent;

                IBrowserContext context = await headlessBrowser.NewContextAsync(contextOptions);
                await AddCookiesAsync(context, cookies);

                IPage page = await context.NewPageAsync();
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });

                return await page.ContentAsync();
            }
            finally
            {
                // 헤드리스 브라우저만 닫는다.
                // 라이브 브라우저에 CloseAsync()를 호출하면 사용자의 브라우저가 종료되므로 절대 호출하지 않는다.
                try { await headlessBrowser.CloseAsync(); } catch { }
            }
        }

        // 쿠키 하나가 Chromium 정책에 맞지 않아도 전체 이식이 실패하지 않도록
        // 전체 추가가 실패하면 개별 쿠키로 재시도한다.
        static async Task AddCookiesAsync(IBrowserContext context, IReadOnlyList<Cookie> cookies)
        {
            if (cookies.Count == 0)
                return;

            try
            {
                await context.AddCookiesAsync(cookies);
            }
            catch
            {
                foreach (Cookie cookie in cookies)
                {
                    try { await context.AddCookiesAsync(new[] { cookie }); } catch { }
                }
            }
        }

        // 라이브 브라우저의 product 문자열(Edg/..., Chrome/...)을 기준으로
        // 같은 실행 파일을 우선 선택한다. 쿠키의 UA/지문 호환성을 높이기 위함이다.
        static string? FindBrowserExecutable(string product)
        {
            string[] edgePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe")
            };

            string[] chromePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            if (product.StartsWith("Edg/", StringComparison.OrdinalIgnoreCase))
                return FindFirstExisting(edgePaths) ?? FindFirstExisting(chromePaths);

            if (product.StartsWith("Chrome/", StringComparison.OrdinalIgnoreCase))
                return FindFirstExisting(chromePaths) ?? FindFirstExisting(edgePaths);

            return FindFirstExisting(edgePaths) ?? FindFirstExisting(chromePaths);
        }

        static string? FindFirstExisting(string[] paths)
        {
            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }

            return null;
        }

        // CDP Target.createTarget로 새 브라우저 창을 생성한다.
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

            throw new Exception("새 브라우저 창을 열지 못했습니다.");
        }
    }
}
