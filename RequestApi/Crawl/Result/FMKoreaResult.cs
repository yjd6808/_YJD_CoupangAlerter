using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class FMKoreaResult : CrawlResult
    {
        public FMBoardType BoardType { get; }

        // =====================================
        public bool TodayPost { get; }                  // 오늘 쓴 글인지

        public FMKoreaResult(FMBoardType boardType, bool todayPost, string url, string title, string name, DateTime writeDate, long viewCount, long recommend) : 
            base(url, title, name, writeDate, viewCount, recommend)
        {
            BoardType = boardType;
            TodayPost = todayPost;
        }
    }
}
