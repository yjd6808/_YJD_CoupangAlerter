using System;
using System.Collections.Generic;
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
            req.Timeout = 1000; 
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";

            // 웹 페이지가 소스가 1MB를 넘어가진 않겠지..?
            byte[] totalRecv = new byte[1024 * 1024];
            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                int readBytes = 0;
                int offset = 0;
                while ((readBytes = responseStream.Read(totalRecv, offset, 1024)) != 0)
                    offset += readBytes;
            }

            content = Encoding.UTF8.GetString(totalRecv);
            return true;
        }
    }
}
