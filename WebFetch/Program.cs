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
        // FM 로그인 세션 저장 파일. exe와 같은 폴더에 둔다.
        // WindowsApp 빌드 시 WebFetch 출력물이 그대로 복사되므로
        // 앱 폴더와 WebFetch 폴더가 같은 파일을 공유한다.
        static string AuthPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fm_auth.json");

        static async Task<int> Main(string[] args)
        {
            // stdout/stderr 라이터가 생성되기 전에 UTF-8로 고정한다.
            // 안 그러면 stderr가 시스템 코드페이지(CP949)로 기록되고,
            // 앱이 UTF-8로 읽으면서 한글 에러 메시지가 깨진다.
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("URL argument required");
                return 1;
            }

            try
            {
                if (args[0] == "--login")
                {
                    if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                    {
                        Console.Error.WriteLine("Usage: WebFetch.exe --login <id> <pw>");
                        return 1;
                    }

                    await LoginAsync(args[1], args[2]);
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Write("LOGIN_OK");
                    return 0;
                }

                // 사람이 직접 보이는 브라우저 창에서 로그인하고 세션만 저장한다.
                // 자동 입력은 FM의 Turnstile을 통과하지 못하므로 이 방식이 표준이다.
                if (args[0] == "--login-manual")
                {
                    await ManualLoginAsync();
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Write("LOGIN_OK");
                    return 0;
                }

                // 실제 설치된 Edge 프로필에서 쿠키를 가져와 fm_auth.json으로 저장한다.
                // Edge에 FM 자동로그인이 되어 있으면 로그인 과정 없이 세션을 확보한다.
                // Edge 실행 중에는 쿠키 파일이 잠겨 있으므로 완전히 종료 후 실행해야 한다.
                if (args[0] == "--import-edge")
                {
                    int imported = await ImportEdgeProfileAsync();
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Write($"IMPORT_OK {imported}");
                    return 0;
                }

                // 실행 중인 실제 Edge에 CDP로 접속해 백그라운드 탭으로 가져온다.
                // Edge는 --remote-debugging-port=9222로 실행되어 있어야 한다.
                // 사용자의 실제 프로필/세션을 그대로 쓰므로 로그인이 따로 필요 없다.
                if (args[0] == "--cdp-fetch")
                {
                    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                    {
                        Console.Error.WriteLine("Usage: WebFetch.exe --cdp-fetch <url>");
                        return 1;
                    }

                    string cdpHtml = await FetchCdpAsync(args[1]);
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Write(cdpHtml);
                    return 0;
                }

                // Edge 프로필 복사본으로 실제 Edge를 headless로 띄워 직접 가져온다.
                // 쿠키 파일→세션 파일 변환 없이 같은 컨텍스트에서 바로 읽기 때문에
                // 세션 전달 손실이 없다.
                if (args[0] == "--fetch-edge")
                {
                    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                    {
                        Console.Error.WriteLine("Usage: WebFetch.exe --fetch-edge <url>");
                        return 1;
                    }

                    string edgeHtml = await FetchEdgeAsync(args[1]);
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Write(edgeHtml);
                    return 0;
                }

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

        static async Task EnsureBrowserAsync()
        {
            string installFlag = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".playwright-installed");
            if (!File.Exists(installFlag))
            {
                int exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                if (exitCode != 0)
                    throw new Exception($"Playwright Chromium 설치 실패 (exit code: {exitCode})");
                File.WriteAllText(installFlag, "installed");
            }
        }

        // 실제 브라우저와 구분하기 어려운 컨텍스트 옵션 (UA/로케일/뷰포트/타임존).
        // 로그인과 크롤링이 같은 지문을 써야 세션이 끊기지 않는다.
        static BrowserNewContextOptions RealisticContextOptions()
        {
            return new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                Locale = "ko-KR",
                TimezoneId = "Asia/Seoul",
                ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
                HasTouch = false,
                IsMobile = false,
                DeviceScaleFactor = 1,
            };
        }

        // 헤드리스 탐지 기본 항목을 가린다 (webdriver/chrome/plugins/languages).
        static async Task MaskAutomationAsync(IBrowserContext context)
        {
            await context.AddInitScriptAsync(@"() => {
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                window.chrome = window.chrome || { runtime: {} };
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3] });
                Object.defineProperty(navigator, 'languages', { get: () => ['ko-KR', 'ko', 'en-US', 'en'] });
            }");
        }

        // FM코리아 로그인 후 세션을 fm_auth.json에 저장한다.
        // 성공 판정은 current_member_srl != 0 으로만 한다 (로그인 벽 문자열은 오판 사례 있음).
        static async Task LoginAsync(string id, string pw)
        {
            await EnsureBrowserAsync();

            using var playwright = await Playwright.CreateAsync();
            // 로그인은 보이는 브라우저로 수행한다. 헤드리스에서는 Turnstile이 무조건 개입한다.
            // 로그인 한 번만 뜨는 창이므로 크롤링(DownloadHtmlAsync)은 계속 헤드리스다.
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
            var contextOptions = RealisticContextOptions();
            var context = await browser.NewContextAsync(contextOptions);
            await MaskAutomationAsync(context);
            var page = await context.NewPageAsync();

            await page.GotoAsync("https://www.fmkorea.com/index.php?mid=stock&act=dispMemberLoginForm", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });

            // 사람처럼 천천히 입력한다 (행동 기반 탐지 완화).
            await page.ClickAsync("#n_uid");
            await page.TypeAsync("#n_uid", id, new PageTypeOptions { Delay = 70, Timeout = 15000 });
            await page.WaitForTimeoutAsync(500);
            await page.ClickAsync("#n_upw");
            await page.TypeAsync("#n_upw", pw, new PageTypeOptions { Delay = 80, Timeout = 15000 });
            await page.WaitForTimeoutAsync(800);

            // 실제 로그인 버튼 클릭으로 제출한다 (일반 사용자 동작과 동일하게).
            // AJAX 로그인일 수도 있어서 내비게이션 대기는 실패 허용하고 settling으로 보완한다.
            var submit = page.Locator("#fo_member_login input[type=submit]");
            if (await submit.CountAsync() == 0)
                throw new Exception("로그인 실패: 로그인 버튼을 찾지 못했습니다 (사이트 구조 변경 가능).");

            try
            {
                await page.RunAndWaitForNavigationAsync(() => submit.First.ClickAsync(),
                    new PageRunAndWaitForNavigationOptions { WaitUntil = WaitUntilState.Load, Timeout = 20000 });
            }
            catch (Exception) { }
            await page.WaitForTimeoutAsync(3000);

            string afterSubmitUrl = page.Url;
            string afterSubmitHtml = await page.ContentAsync();
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_submit.html"), $"<!-- url: {afterSubmitUrl} -->\n" + afterSubmitHtml); } catch { }

            // 세션이 실제로 인증됐는지 게시판에서 회원 번호로 확정 판정한다.
            await page.GotoAsync("https://www.fmkorea.com/stock", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });
            string html = await page.ContentAsync();
            long memberSrl = ParseMemberSrl(html);

            if (memberSrl == 0)
            {
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_debug.html"), html); } catch { }
                if (html.Contains("captcha") || html.Contains("Captcha") || html.Contains("recaptcha"))
                    throw new Exception("로그인 실패: 캡차가 요구됩니다. 일반 브라우저에서 직접 로그인 후 다시 시도하세요.");
                throw new Exception($"로그인 실패: 세션이 익명으로 남았습니다 (제출 후 URL: {afterSubmitUrl}). 아이디/비밀번호를 확인하세요. 응답 HTML을 login_submit.html, login_debug.html에 저장했습니다.");
            }

            await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = AuthPath });
        }

        // 보이는 브라우저에 FM 로그인 폼을 띄우고 사용자가 직접 로그인할 때까지 기다린다.
        // Turnstile 등 자동화 검사는 사람이 직접 해소한다. 최대 5분 대기한다.
        static async Task ManualLoginAsync()
        {
            await EnsureBrowserAsync();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
            var context = await browser.NewContextAsync(RealisticContextOptions());
            await MaskAutomationAsync(context);
            var page = await context.NewPageAsync();

            await page.GotoAsync("https://www.fmkorea.com/index.php?mid=stock&act=dispMemberLoginForm", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });

            for (int i = 0; i < 150; i++)
            {
                await page.WaitForTimeoutAsync(2000);

                string html;
                try { html = await page.ContentAsync(); }
                catch (Exception) { continue; } // 창을 닫았거나 이동 중이면 다음 폴링으로

                if (ParseMemberSrl(html) != 0)
                {
                    await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = AuthPath });
                    return;
                }
            }

            throw new Exception("로그인 제한 시간 초과 (5분). 브라우저 창에서 로그인을 완료하지 못했습니다.");
        }

        // Edge 프로필 최소 복사본으로 실제 Edge를 headless로 띄워 세션을 저장한다.
        // 복사본: Local State(DPAPI 키) + Default/Network/Cookies. 원본은 건드리지 않는다.
        // 반환값: 저장된 쿠키 개수.
        static async Task<int> ImportEdgeProfileAsync()
        {
            return await RunWithEdgeCopyAsync(async context =>
            {
                var page = await context.NewPageAsync();
                await page.GotoAsync("https://www.fmkorea.com/stock", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });

                var cookies = await context.CookiesAsync();
                await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = AuthPath });

                int fmCount = 0;
                foreach (var c in cookies)
                {
                    if (c.Domain != null && c.Domain.EndsWith("fmkorea.com"))
                        fmCount++;
                }

                if (fmCount == 0)
                    throw new Exception("Edge 프로필에서 fmkorea 쿠키를 찾지 못했습니다. Edge에서 FM에 로그인되어 있는지 확인하세요.");

                return cookies.Count;
            });
        }

        // Edge 프로필 최소 복사본을 만든다. 실행 중인 Edge의 잠긴 파일도
        // 공유 읽기로 복사하므로 Edge를 종료할 필요가 없다.
        // 매번 고유 폴더를 써서 이전 실행 잔재와 잠금 충돌을 피운다.
        // Edge를 완전히 종료한다. 창 닫기 요청 후 남으면 강제 종료한다.
        // 쿠키 파일 잠금을 풀기 위해 프로필 복사 전에 반드시 호출한다.
        static void KillEdge()
        {
            System.Diagnostics.Process[] procs;
            try { procs = System.Diagnostics.Process.GetProcessesByName("msedge"); }
            catch { return; }

            if (procs.Length == 0)
                return;

            foreach (var p in procs)
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                        p.CloseMainWindow();
                }
                catch { }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 15000)
            {
                try
                {
                    if (System.Diagnostics.Process.GetProcessesByName("msedge").Length == 0)
                        return;
                }
                catch { return; }
                System.Threading.Thread.Sleep(500);
            }

            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("msedge"))
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }

            System.Threading.Thread.Sleep(2000);

            if (System.Diagnostics.Process.GetProcessesByName("msedge").Length != 0)
                throw new Exception("Edge 프로세스가 남아 있습니다. 작업 관리자에서 msedge를 직접 종료하세요.");
        }

        static string CopyEdgeProfile()
        {
            KillEdge();

            string edgeData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Edge\User Data");
            string profileCopy = Path.Combine(Path.GetTempPath(), "CoupangAlerterEdgeProfile_" + Guid.NewGuid().ToString("N"));

            CleanupOldEdgeCopies();

            // 죽어가는 Edge 프로세스가 핸들을 잠시 들고 있을 수 있어서
            // Kill 후 최대 4회까지 재시도한다.
            Exception lastError = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                KillEdge();

                try
                {
                    Directory.CreateDirectory(Path.Combine(profileCopy, "Default", "Network"));
                    CopyFileShared(Path.Combine(edgeData, "Local State"), Path.Combine(profileCopy, "Local State"), false);
                    CopyFileShared(Path.Combine(edgeData, "Default", "Network", "Cookies"),
                        Path.Combine(profileCopy, "Default", "Network", "Cookies"), true);
                    return profileCopy;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lastError = ex;
                }

                try { Task.Delay(2000).Wait(); } catch { }
            }

            throw new Exception("Edge 쿠키 파일을 읽지 못했습니다 (" + lastError?.Message + "). Edge를 완전히 종료(Edge 완전 종료 버튼)한 뒤 다시 시도하세요.");
        }

        static void CleanupOldEdgeCopies()
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(Path.GetTempPath(), "CoupangAlerterEdgeProfile_*"))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
        }

        // 다른 프로세스가 열어둔 파일도 공유 읽기로 복사한다. 최대 3회 재시도한다.
        // validateSqlite가 true면 복사본이 온전한 SQLite DB인지 검사한다.
        static void CopyFileShared(string src, string dst, bool validateSqlite = false)
        {
            Exception lastError = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var fin = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var fout = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fin.CopyTo(fout);
                    }

                    if (validateSqlite)
                        ValidateSQLite(dst);
                    return;
                }
                catch (Exception ex) { lastError = ex; }

                try { Task.Delay(500).Wait(); } catch { }
            }

            throw lastError ?? new IOException("파일 복사 실패: " + src);
        }

        static void ValidateSQLite(string path)
        {
            byte[] magic = new byte[16];
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(magic, 0, magic.Length) != magic.Length)
                    throw new IOException("파일이 너무 작습니다: " + path);
            }

            string header = Encoding.ASCII.GetString(magic);
            if (header != "SQLite format 3\0")
                throw new IOException("SQLite 파일이 아닙니다 (읽기 중 변경됨): " + path);
        }

        // Edge 프로필 복사본으로 실제 Edge(headless)를 띄워 action을 실행한다.
        // playwright 수명과 context 수명을 함께 관리해야 해서 헬퍼로 묶는다.
        static async Task<T> RunWithEdgeCopyAsync<T>(Func<IBrowserContext, Task<T>> action)
        {
            await EnsureBrowserAsync();
            string profileCopy = CopyEdgeProfile();

            using var playwright = await Playwright.CreateAsync();
            await using var context = await playwright.Chromium.LaunchPersistentContextAsync(profileCopy,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = "msedge",
                    Headless = true,
                    Args = new[] { "--disable-blink-features=AutomationControlled" },
                    Timeout = 60000,
                });
            return await action(context);
        }

        static async Task<string> FetchCdpAsync(string url)
        {
            await EnsureBrowserAsync();

            using var playwright = await Playwright.CreateAsync();

            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222",
                    new BrowserTypeConnectOverCDPOptions { Timeout = 10000 });
            }
            catch (Exception)
            {
                throw new Exception("실행 중인 Edge에 접속하지 못했습니다. Edge를 --remote-debugging-port=9222 옵션으로 실행하세요.");
            }

            if (browser.Contexts.Count == 0)
                throw new Exception("Edge에 열린 브라우저 컨텍스트가 없습니다.");

            // using 종료 시 CDP 연결만 끊기고 실제 Edge는 계속 실행된다.
            var context = browser.Contexts[0];

            // 이미 열려 있는 FM 탭이 있으면 그 탭의 DOM을 그대로 읽는다.
            // 새 탭을 만들지 않으므로 화면에 아무 변화가 없다.
            foreach (var existing in context.Pages)
            {
                try
                {
                    string existingUrl = existing.Url ?? "";
                    if (existingUrl.Contains("fmkorea.com"))
                        return await existing.ContentAsync();
                }
                catch (Exception) { }
            }

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

        static async Task<string> FetchEdgeAsync(string url)
        {
            return await RunWithEdgeCopyAsync(async context =>
            {
                var page = await context.NewPageAsync();
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });
                return await page.ContentAsync();
            });
        }

        static long ParseMemberSrl(string html)
        {
            var memberMatch = System.Text.RegularExpressions.Regex.Match(html ?? "", @"current_member_srl\s*=\s*(\d+)");
            return memberMatch.Success ? long.Parse(memberMatch.Groups[1].Value) : 0;
        }

        // 로그인 벽 판정. 주의: dispMemberLoginForm/procMemberLogin 문자열은
        // 정상 게시판 페이지의 헤더 로그인 위젯에도 들어있어서 판별자로 쓰면 안 된다.
        // 실제 로그인 폼(form id="fo_member_login") 존재 여부로 판정한다.
        static bool IsLoginWall(string html)
        {
            return html != null && html.Contains("fo_member_login") && html.Contains("procMemberLogin");
        }

        static async Task<string> DownloadHtmlAsync(string url)
        {
            await EnsureBrowserAsync();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var contextOptions = RealisticContextOptions();
            if (File.Exists(AuthPath))
                contextOptions.StorageStatePath = AuthPath;

            var context = await browser.NewContextAsync(contextOptions);
            await MaskAutomationAsync(context);
            var page = await context.NewPageAsync();

            // FM은 첫 요청에 세션 확인용 쿠키를 심고 다음 요청부터 회원 세션을
            // 인정한다. 한 컨텍스트에서 예열 후 목표 페이지로 이동한다.
            try
            {
                await page.GotoAsync("https://www.fmkorea.com/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout = 30000
                });
                await page.WaitForTimeoutAsync(2000);
            }
            catch (Exception) { }

            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });
            return await page.ContentAsync();
        }
    }
}
