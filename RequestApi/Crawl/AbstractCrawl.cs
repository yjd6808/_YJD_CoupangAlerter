using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using HtmlAgilityPack;
using RequestApi.Crawl.Result;

namespace RequestApi.Crawl
{
    public abstract class AbstractCrawl
    {
        public CrawlType CrawlType { get; }
        public Uri CrawlUrl { get; }

        protected AbstractCrawl(Uri reqUrl, CrawlType crawlType)
        {
            CrawlUrl = reqUrl;
        }

        protected AbstractCrawl(string reqUrl, CrawlType crawlType)
        {
            CrawlUrl = new Uri(reqUrl);
        }

        protected abstract List<CrawlResult> ParseContent(HtmlDocument document);

        // 실패시 null
        public List<CrawlResult> TryCrawl()
        {
            try
            {
                string content = "";

                if (!TryDonwloadSource(out content))
                    return null;

                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(content);
                return ParseContent(document);
            }
            catch
            {
#if DEBUG
                throw;
#else
                return null;
#endif
            }
        }

        

        protected bool TryDonwloadSource(out string content)
        {
            content = "";

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(CrawlUrl);
            req.Method = "GET";
            req.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";

            byte[] totalRecv = new byte[1024 * 1024];

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            {
                HttpStatusCode status = response.StatusCode;

                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                        return false;

                    int readBytes = 0;
                    int offset = 0;
                    while ((readBytes = responseStream.Read(totalRecv, offset, 1024)) != 0)
                        offset += readBytes;
                }
            }

            content = Encoding.UTF8.GetString(totalRecv);
            return true;
        }

        
    }
}
