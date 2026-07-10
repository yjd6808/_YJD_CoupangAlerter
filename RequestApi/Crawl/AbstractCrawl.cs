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
using RequestApi.Crawl.Result;

namespace RequestApi.Crawl
{
    public abstract class AbstractCrawl
    {
        public static WebCrawlMethod Method { get; set; } = WebCrawlMethod.AppWebRequest;

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
                    return null;

                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(content);
                return ParseContent(document);
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return null;
            }
        }

        

        protected bool TryDonwloadSource(out string content)
        {
            if (Method == WebCrawlMethod.ExternalAppWebRequest)
                return TryDownloadSourceExternal(out content);

            return TryDownloadSourceDirect(out content);
        }

        private bool TryDownloadSourceDirect(out string content)
        {
            content = "";

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(CrawlUrl);
            req.Method = "GET";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    return false;

                using (MemoryStream ms = new MemoryStream())
                {
                    responseStream.CopyTo(ms);
                    content = Encoding.UTF8.GetString(ms.ToArray());
                }
            }

            return true;
        }

        private bool TryDownloadSourceExternal(out string content)
        {
            content = "";

            try
            {
                string webFetchPath = Path.Combine(Environment.CurrentDirectory, "WebFetch.exe");
                if (!File.Exists(webFetchPath))
                    return false;

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = webFetchPath,
                    Arguments = CrawlUrl.AbsoluteUri,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (Process p = Process.Start(psi))
                {
                    content = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                    return p.ExitCode == 0 && content.Length > 0;
                }
            }
            catch
            {
                content = "";
                return false;
            }
        }

        public abstract AbstractCrawl Clone();
    }
}
