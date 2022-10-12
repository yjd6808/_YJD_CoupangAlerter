using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class CrawlResult
    {
        public string Url { get; }              // 하이퍼링크
        public string Title { get; }            // 제목
        public string Name { get; }             // 글쓴이
        public DateTime WriteDate { get; }      // 작성일
        public long ViewCount { get; }          // 조회
        public long Recommend { get; }          // 추천
        


        public CrawlResult(string url, string title, string name, DateTime writeDate, long viewCount, long recommend)
        {
            Url = url;
            Title = title;
            Name = name;
            WriteDate = writeDate;
            ViewCount = viewCount;
            Recommend = recommend;
        }

    }
}
