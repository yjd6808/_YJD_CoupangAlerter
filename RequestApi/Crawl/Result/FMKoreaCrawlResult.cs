/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class FMKoreaCrawlResult : CrawlResult
    {
        public FMBoardType BoardType { get; }
        // =====================================
        public bool TodayPost { get; }                  // 오늘 쓴 글인지

        public FMKoreaCrawlResult(long postId, FMBoardType boardType, bool todayPost, string url, string title, string name, DateTime writeDate, long viewCount, long recommend) : 
            base(postId, url, title, name, writeDate, viewCount, recommend, Crawl.CrawlType.FMKorea)
        {
            BoardType = boardType;
            TodayPost = todayPost;
        }
    }
}
