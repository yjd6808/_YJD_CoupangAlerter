/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 에펨코리아 주식 크롤링
 * * * * * * * * * * * * * * 
 */

using RequestApi.Crawl.Result;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;
using HtmlAgilityPack;
using RequestApi.Util;
using System.Web;

namespace RequestApi.Crawl
{
    public enum FMBoardType
    {
        전체,
        인기,
        잡담,
        국내주식,
        해외주식,
        질문,
        종목추천_분석,
        정보공유,
        이벤트
    }

    public enum FMSearchOption
    {
        NickName,           // 닉네임
        TitleContent,       // 제목 + 내용
        Title,              // 제목
        Content,            // 내용
        Comment,            // 댓글
        None,
    }

    public class FMKoreaCrawl : AbstractCrawl
    {
        public FMBoardType BoardType { get; }
        public FMSearchOption SearchOption { get; }
        public string SearchContent { get; }
        public int Page { get; }
        

        // ====================================================================

        public static readonly string MainUrl = @"https://www.fmkorea.com";
        public static readonly string CrawlBaseUrl = $"{MainUrl}/index.php?mid=stock";


        private static readonly Dictionary<FMBoardType, string> _categoryIdMap = new Dictionary<FMBoardType, string>()
        {
            { FMBoardType.전체, "0" },
            { FMBoardType.인기, "1" },
            { FMBoardType.잡담, "196357514" },
            { FMBoardType.국내주식, "2997203870" },
            { FMBoardType.해외주식, "2997204381" },
            { FMBoardType.질문, "196466085" },
            { FMBoardType.종목추천_분석, "196459220" },
            { FMBoardType.정보공유, "196633079" },
            { FMBoardType.이벤트, "2061959383" },
        };

        private static readonly Dictionary<string, FMBoardType> _categoryNameMap = new Dictionary<string, FMBoardType>()
        {
            { "잡담", FMBoardType.잡담 },
            { "국내주식", FMBoardType.국내주식 },
            { "해외주식", FMBoardType.해외주식 },
            { "질문", FMBoardType.질문 },
            { "종목추천,분석", FMBoardType.종목추천_분석 },
            { "정보공유", FMBoardType.정보공유 },
            { "이벤트", FMBoardType.이벤트 },
        };

        private static readonly Dictionary<FMSearchOption, string> _searchOptionMap = new Dictionary<FMSearchOption, string>()
        {
            { FMSearchOption.TitleContent, "title_content" },
            { FMSearchOption.Title, "title" },
            { FMSearchOption.Content, "content" },
            { FMSearchOption.Comment, "comment" },
            { FMSearchOption.NickName, "nick_name" },
        };


        public FMKoreaCrawl(FMBoardType boardType = FMBoardType.전체, int page = 1) : 
            base(ApplyCrawlOption(boardType, page, FMSearchOption.None, ""), CrawlType.FMKorea)
        {
            BoardType = boardType;
            Page = page;
            SearchOption = FMSearchOption.None;
            SearchContent = "";
        }

        public FMKoreaCrawl(FMSearchOption searchOption, string searchContent, FMBoardType boardType = FMBoardType.전체, int page = 1) : 
            base(ApplyCrawlOption(boardType, page, searchOption, searchContent), CrawlType.FMKorea)
        {
            SearchOption = searchOption;
            SearchContent = searchContent;
            BoardType = boardType;
            Page = page;
        }


        private static string ApplyCrawlOption(FMBoardType boardType, int page, FMSearchOption searchOption, string searchContent)
        {
            string reqUrl = CrawlBaseUrl;

            if (boardType != FMBoardType.전체)
            {
                if (boardType == FMBoardType.인기)
                    reqUrl += "&sort_index=pop&order_type=desc&listStyle=list";
                else
                    reqUrl += $"&category={_categoryIdMap[boardType]}";
            }
                

            if (page > 1)
                reqUrl += $"&page={page}";

            if (searchOption != FMSearchOption.None && searchContent != "")
                reqUrl += $"&search_keyword={HttpUtility.UrlEncode(searchContent, Encoding.UTF8)}&search_target={_searchOptionMap[searchOption]}";

            return reqUrl;
        }
        protected override List<CrawlResult> ParseContent(HtmlDocument document)
        {
            List<CrawlResult> results = new List<CrawlResult>();
            HtmlNodeCollection collection = document.DocumentNode.SelectNodes("//table/tbody/tr");

            if (collection == null)
                return null;

            foreach (HtmlNode node in collection)
            {
                string boardType_ = node.SelectSingleNode("td[@class='cate']")?.InnerText.Replace("\t", "");
                string title = node.SelectSingleNode("td[contains(@class, 'title')]")?.InnerText.Replace("\t", "");
                string name = node.SelectSingleNode("td[@class='author']")?.InnerText.Trim();
                string time = node.SelectSingleNode("td[@class='time']")?.InnerText.Trim();
                string viewCount_ = node.SelectSingleNode("td[@class='m_no']")?.InnerText.JoinAllNumber();
                string recommendCount_ = node.SelectSingleNode("td[contains(@class, 'm_no_voted')]")?.InnerText.JoinAllNumber();
                string url = node.SelectSingleNode("td[contains(@class, 'title')]")?.SelectSingleNode("a[1]")?.GetAttributeValue("href", "");

                if (boardType_ == null) continue;
                if (title == null) continue;
                if (name == null) continue;
                if (time == null) continue;
                if (viewCount_ == null) continue;

                string postId_ = "";

                NameValueCollection queryCollection = HttpUtility.ParseQueryString(url);
                var enumerator = queryCollection.GetEnumerator();
                bool foundId = false;
                while (enumerator.MoveNext())
                {
                    string strKey = enumerator.Current as string;
                    if (strKey == null) continue;
                    if (strKey == "amp;document_srl")
                    {
                        postId_ = queryCollection.Get("amp;document_srl");
                        foundId = true;
                        break;
                    }

                    if (strKey == "document_srl")
                    {
                        postId_ = queryCollection.Get("document_srl");
                        foundId = true;
                        break;
                    }
                }

                if (!foundId && url.StartsWith("/"))
                    postId_ = url.JoinAllNumber();

                long.TryParse(viewCount_, out long viewCount);
                long.TryParse(postId_, out long postId);
                long.TryParse(recommendCount_, out long recommendCount);

                if (postId == 0)
                    throw new Exception($"포스트 아이디를 얻을 수가 없습니다. {url}");

                DateTime writeDate = DateTime.Now;
                bool todayPost = false;

                if (time.Length == 5) // 00:00 => 5글자
                {
                    int hour = int.Parse(time.Split(':')[0]);
                    int minute = int.Parse(time.Split(':')[1]);

                    writeDate = new DateTime(
                        writeDate.Year, 
                        writeDate.Month, 
                        writeDate.Day,
                        hour,
                        minute,
                        0);
                    todayPost = true;
                }
                else if (time.Length == 10) // 2020.10.11 => 10글자
                {
                    // 이전날에 작성된 경우
                    int year = int.Parse(time.Split('.')[0]);
                    int month = int.Parse(time.Split('.')[1]);
                    int day = int.Parse(time.Split('.')[2]);

                    writeDate = new DateTime(year, month, day);
                }

                _categoryNameMap.TryGetValue(boardType_, out FMBoardType boardType);

                results.Add(new FMKoreaCrawlResult(
                    postId,
                    boardType,
                    todayPost,
                    MainUrl + url,
                    title,
                    name,
                    writeDate,
                    viewCount,
                    recommendCount
                ));
            }

            return results;
        }

        public override AbstractCrawl Clone()
        {
            return new FMKoreaCrawl(SearchOption, SearchContent, BoardType, Page);
        }
    }
}

