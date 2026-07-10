/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace WebFetch
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 18563;

            if (args.Length > 0 && int.TryParse(args[0], out int customPort))
                port = customPort;

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            Console.WriteLine($"[WebFetch] Listening on http://localhost:{port}/");

            while (true)
            {
                try
                {
                    HttpListenerContext ctx = listener.GetContext();
                    ProcessRequest(ctx);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WebFetch] Error: {ex.Message}");
                }
            }
        }

        static void ProcessRequest(HttpListenerContext ctx)
        {
            try
            {
                string body;
                using (StreamReader reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    body = reader.ReadToEnd();

                var request = JsonSerializer.Deserialize<FetchRequest>(body);
                if (request == null || string.IsNullOrWhiteSpace(request.Url))
                {
                    WriteResponse(ctx, new FetchResponse { Error = "Invalid request: url required" });
                    return;
                }

                string html = DownloadHtml(request.Url);
                WriteResponse(ctx, new FetchResponse { Content = html });
            }
            catch (Exception ex)
            {
                WriteResponse(ctx, new FetchResponse { Error = ex.Message });
            }
        }

        static string DownloadHtml(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36";
            req.Timeout = 30000;

            byte[] totalRecv = new byte[1024 * 1024];
            int offset = 0;

            using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                if (responseStream == null)
                    throw new Exception("No response stream");

                int readBytes;
                while ((readBytes = responseStream.Read(totalRecv, offset, 1024)) != 0)
                    offset += readBytes;
            }

            return Encoding.UTF8.GetString(totalRecv, 0, offset);
        }

        static void WriteResponse(HttpListenerContext ctx, FetchResponse resp)
        {
            string json = JsonSerializer.Serialize(resp);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }
    }

    class FetchRequest
    {
        public string Url { get; set; }
    }

    class FetchResponse
    {
        public string Content { get; set; }
        public string Error { get; set; }
    }
}
