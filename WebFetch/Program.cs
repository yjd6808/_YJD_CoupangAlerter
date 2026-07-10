/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.IO;
using System.Net;
using System.Text;

namespace WebFetch
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("URL argument required");
                return 1;
            }

            try
            {
                string html = DownloadHtml(args[0]);
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

        static string DownloadHtml(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";
            req.Timeout = 30000;

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    throw new Exception("No response stream");

                using (MemoryStream ms = new MemoryStream())
                {
                    responseStream.CopyTo(ms);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
    }
}
