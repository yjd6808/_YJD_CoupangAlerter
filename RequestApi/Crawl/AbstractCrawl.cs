/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RequestApi.Crawl.Result;

namespace RequestApi.Crawl
{
    public abstract class AbstractCrawl
    {
        public static WebCrawlMethod Method { get; set; } = WebCrawlMethod.AppWebRequest;
        public const int WebFetchPort = 18563;

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

            // 웹 페이지가 소스가 1MB를 넘어가진 않겠지..?
            byte[] totalRecv = new byte[1024 * 1024];
            int offset = 0;

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {

                if (responseStream == null)
                    return false;

                int readBytes = 0;

                while ((readBytes = responseStream.Read(totalRecv, offset, 1024)) != 0)
                    offset += readBytes;
            }

            content = Encoding.UTF8.GetString(totalRecv, 0, offset);
            return true;
        }

        private bool TryDownloadSourceExternal(out string content)
        {
            content = "";

            try
            {
                string fetchUrl = $"http://localhost:{WebFetchPort}/";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(fetchUrl);
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.UserAgent = "CoupangAlerter/1.0";
                req.Timeout = 30000;

                string requestBody = JsonConvert.SerializeObject(new { url = CrawlUrl.AbsoluteUri });
                byte[] requestBodyBytes = Encoding.UTF8.GetBytes(requestBody);
                req.ContentLength = requestBodyBytes.Length;

                using (Stream stream = req.GetRequestStream())
                    stream.Write(requestBodyBytes, 0, requestBodyBytes.Length);

                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    string responseJson = reader.ReadToEnd();
                    JObject obj = JObject.Parse(responseJson);

                    if (obj["error"] != null)
                    {
                        content = "";
                        return false;
                    }

                    content = obj["content"]?.ToString() ?? "";
                    return true;
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
