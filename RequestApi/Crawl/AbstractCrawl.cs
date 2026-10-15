/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using RequestApi.Crawl.Result;

namespace RequestApi.Crawl
{
    public abstract class AbstractCrawl
    {
        public static WebCrawlMethod Method { get; set; } = WebCrawlMethod.AppWebRequest;

        // FM 로그인 세션 파일명. exe와 같은 폴더에서 찾는다.
        // WebFetch.exe --login으로 생성한다.
        public static string AuthFileName { get; set; } = "fm_auth.json";

        // 로그인 벽 판정. 주의: dispMemberLoginForm/procMemberLogin 문자열은
        // 정상 게시판 페이지의 헤더 로그인 위젯에도 들어있어서 판별자로 쓰면 안 된다.
        // 실제 로그인 폼(form id="fo_member_login") 존재 여부로 판정한다.
        public static bool IsLoginWall(string html)
        {
            return html != null && html.Contains("fo_member_login") && html.Contains("procMemberLogin");
        }

        public int Type { get; }
        public Uri CrawlUrl { get; }

        protected AbstractCrawl(Uri reqUrl, int type)
        {
            CrawlUrl = reqUrl;
            Type = type;
        }

        protected AbstractCrawl(string reqUrl, int type)
        {
            CrawlUrl = new Uri(reqUrl);
            Type = type;
        }

        public T To<T>() where T : AbstractCrawl
        {
            return (T)this;
        }

        protected abstract List<CrawlResult> ParseContent(HtmlDocument document);

        // 실패시 null
        public List<CrawlResult> TryCrawl(out string errorMessage)
        {
            errorMessage = "";

            try
            {
                string content = "";

                if (!TryDonwloadSource(out content))
                {
                    errorMessage = $"[{CrawlType.ToStringInitial(Type)}] HTML 다운로드 실패";
                    return null;
                }

                if (Type == CrawlType.FMKorea && IsLoginWall(content))
                    throw new Exception(GetFMLoginWallMessage());

                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(content);
                List<CrawlResult> parsed = ParseContent(document);

                if (parsed == null)
                {
                    errorMessage = $"[{CrawlType.ToStringInitial(Type)}] 파싱 결과가 null입니다 (게시글 목록을 찾지 못함)";
                    return null;
                }

                return parsed;
            }
            catch (Exception e)
            {
                errorMessage = $"[{CrawlType.ToStringInitial(Type)}] {e.Message}";
                return null;
            }
        }

        

        private static string GetFMLoginWallMessage()
        {
            try
            {
                string authPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AuthFileName);
                if (!File.Exists(authPath))
                    return "저장된 FM 세션이 없습니다. Edge에서 FM에 로그인한 뒤 프로그램 설정 > Edge 세션 가져오기로 세션을 저장하세요.";
            }
            catch { }

            return "저장된 FM 세션이 만료되었거나 무효합니다. Edge에서 FM에 로그인한 뒤 프로그램 설정 > Edge 세션 가져오기로 세션을 다시 저장하세요.";
        }

        // Edge 직접 제어(UIA)용 HTML 공급자. WindowsApp이 시작 시 연결한다.
        // Android 등 미지원 환경에서는 null이므로 명확한 예외를 던진다.
        public static Func<Uri, string> EdgeUiFetchHandler { get; set; }

        protected bool TryDonwloadSource(out string content)
        {
            if (Method == WebCrawlMethod.ExternalAppWebRequest)
                return TryDownloadSourceExternal(out content);

            if (Method == WebCrawlMethod.EdgeDirectUIA)
                return TryDownloadSourceEdgeUIA(out content);

            return TryDownloadSourceDirect(out content);
        }

        private bool TryDownloadSourceEdgeUIA(out string content)
        {
            content = "";

            var handler = EdgeUiFetchHandler;
            if (handler == null)
                throw new Exception("Edge 직접 제어는 Windows에서만 지원됩니다.");

            content = handler(CrawlUrl);

            if (string.IsNullOrEmpty(content))
                throw new Exception("Edge에서 빈 결과를 받았습니다.");

            return true;
        }

        private bool TryDownloadSourceDirect(out string content)
        {
            content = "";

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(CrawlUrl);
            req.Method = "GET";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";
            req.CookieContainer = BuildCookieContainer();

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    throw new Exception("응답 스트림이 null입니다.");

                using (MemoryStream ms = new MemoryStream())
                {
                    responseStream.CopyTo(ms);
                    content = Encoding.UTF8.GetString(ms.ToArray());
                }
            }

            return true;
        }

        // fm_auth.json(Playwright storage state)의 쿠키를 직접 요청에 싣는다.
        // 로그인 없이 요청하면 FM 검색은 로그인 벽 페이지만 반환하므로,
        // 파일이 없으면 그냥 익명으로 요청하고 TryCrawl의 로그인 벽 검사가 잡아낸다.
        private CookieContainer BuildCookieContainer()
        {
            var jar = new CookieContainer();

            if (Type != CrawlType.FMKorea)
                return jar;

            try
            {
                string authPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AuthFileName);
                if (!File.Exists(authPath))
                    return jar;

                JArray cookies = JObject.Parse(File.ReadAllText(authPath))["cookies"] as JArray;
                if (cookies == null)
                    return jar;

                foreach (JToken c in cookies)
                {
                    string name = (string)c["name"];
                    string value = (string)c["value"] ?? "";
                    string domain = ((string)c["domain"] ?? "").TrimStart('.');
                    string path = (string)c["path"] ?? "/";

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain))
                        continue;

                    try { jar.Add(new Cookie(name, value, path, domain)); } catch { }
                }
            }
            catch { }

            return jar;
        }

        // WebFetch/Playwright 출력에 섞이는 ANSI 색상 코드를 제거한다.
        private static string CleanProcessOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return output;

            return System.Text.RegularExpressions.Regex.Replace(output, "\x1B\\[[0-9;]*[A-Za-z]", "").Trim();
        }

        private bool TryDownloadSourceExternal(out string content)
        {
            content = "";

            string webFetchPath = Path.Combine(Environment.CurrentDirectory, "WebFetch.exe");
            if (!File.Exists(webFetchPath))
                throw new Exception($"WebFetch.exe를 찾을 수 없습니다: {webFetchPath}");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = webFetchPath,
                Arguments = CrawlUrl.AbsoluteUri,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process p = Process.Start(psi))
            {
                content = p.StandardOutput.ReadToEnd();
                string stdErr = p.StandardError.ReadToEnd();
                p.WaitForExit(30000);

                if (p.ExitCode != 0)
                    throw new Exception($"WebFetch 종료코드 {p.ExitCode}: {CleanProcessOutput(stdErr)}");

                if (content.Length == 0)
                    throw new Exception("WebFetch가 빈 결과를 반환했습니다.");

                return true;
            }
        }

        public abstract AbstractCrawl Clone();
    }
}
