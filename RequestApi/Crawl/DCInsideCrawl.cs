// DC 인사이드 미국 주식 크롤링

using RequestApi.Crawl.Result;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using HtmlAgilityPack;

namespace RequestApi.Crawl
{
    public enum DCBoardType
    {
        전체글,
        개념글
    }

    public class DCInsideCrawl : AbstractCrawl
    {
        public int Page { get; }
        public DCBoardType BoardType { get; }
        public static readonly string MainUrl = @"https://gall.dcinside.com";
        public static readonly string CrawlBaseUrl = $"{MainUrl}/mgallery/board/lists?id=stockus";
            
        public DCInsideCrawl(DCBoardType boardType = DCBoardType.전체글, int page = 1) : base(ApplyCrawlOption(boardType, page), CrawlType.DCInside)
        {
            BoardType = boardType;
            Page = page;
        }

        private static string ApplyCrawlOption(DCBoardType boardType, int page)
        {
            string reqUrl = CrawlBaseUrl;

            if (boardType == DCBoardType.개념글)
                reqUrl += "&exception_mode=recommend";

            if (page > 1)
                reqUrl += $"&page={page}";

            return reqUrl;
        }

        protected override List<CrawlResult> ParseContent(HtmlDocument document)
        {
            List<CrawlResult> results = new List<CrawlResult>();
            // HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//tr[@class='ub-content us-post']"); // 아래꺼랑 같은 듯?
            HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//tr[contains(@class, 'ub-content') and contains(@class, 'us-post')]");

            if (collection == null)
                return null;
           
            foreach (HtmlNode node in collection)
            {
                string no_ = node.SelectSingleNode("td[@class='gall_num']")?.InnerText;
                string header = node.SelectSingleNode("td[@class='gall_subject']")?.InnerText;
                string title = node.SelectSingleNode("td[contains(@class, 'gall_tit')]")?.InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
                string name = node.SelectSingleNode("td[@class='gall_writer ub-writer']")?.GetAttributeValue("data-nick", "");
                string ip = node.SelectSingleNode("td[@class='gall_writer ub-writer']")?.GetAttributeValue("data-ip", "");
                string userId = node.SelectSingleNode("td[@class='gall_writer ub-writer']")?.GetAttributeValue("data-uid", "");
                string writeDate = node.SelectSingleNode("td[@class='gall_date']")?.GetAttributeValue("title", "");
                string viewCount_ = node.SelectSingleNode("td[@class='gall_count']")?.InnerText;
                string recommendCount_ = node.SelectSingleNode("td[@class='gall_recommend']")?.InnerText;
                string url = node.SelectSingleNode("td[contains(@class, 'gall_tit')]/a[1]")?.GetAttributeValue("href", ""); 

                if (title == null) continue;
                if (name == null) continue;
                if (writeDate == null) continue;
                if (viewCount_ == null) continue;
                if (recommendCount_ == null) continue;
                if (userId == null) userId = "";
                if (url == null) url = "";

                long.TryParse(no_, out long no);
                long.TryParse(viewCount_, out long viewCount);
                long.TryParse(recommendCount_, out long recommendCount);

                bool isAnonymous = ip != null;
                
                results.Add(new DCInsideCrawlResult(
                        no,
                        header,
                        MainUrl + url,
                        title,
                        name,
                        DateTime.Parse(writeDate),
                        viewCount,
                        recommendCount,
                        isAnonymous,
                        userId
                    )
                );
            }

            return results;
        }
    }
}

