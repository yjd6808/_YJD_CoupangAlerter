/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RequestApi.Crawl.Result;

namespace RequestApi.Crawl
{
    public abstract class AbstractCrawl
    {
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

                if (!TryDownloadSource(out content))
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
            return "엣지 브라우저에서 에펨코리아에 로그인되어 있지 않습니다. 엣지에서 로그인한 뒤 다시 시도하세요.";
        }

        // WebFetch.exe(Playwright)로 크롤링한다.
        // WebFetch는 실행 중인 엣지(9222 포트)에 CDP로 접속해 HTML을 가져온다.
        private bool TryDownloadSource(out string content)
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
                p.WaitForExit(60000);

                if (!p.HasExited)
                {
                    try { p.Kill(); } catch { }
                    throw new Exception("WebFetch 응답 시간 초과 (60초)");
                }

                if (p.ExitCode != 0)
                    throw new Exception($"WebFetch 종료코드 {p.ExitCode}: {CleanProcessOutput(stdErr)}");

                if (content.Length == 0)
                    throw new Exception("WebFetch가 빈 결과를 반환했습니다.");

                return true;
            }
        }

        // WebFetch/Playwright 출력에 섞이는 ANSI 색상 코드를 제거한다.
        private static string CleanProcessOutput(string output)
        {
            if (string.IsNullOrEmpty(output))
                return output;

            return Regex.Replace(output, "\x1B\\[[0-9;]*[A-Za-z]", "").Trim();
        }

        public abstract AbstractCrawl Clone();
    }
}
