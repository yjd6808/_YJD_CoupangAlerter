/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class CrawlResult
    {
        public int CrawlType { get; }
        public long PostId { get; }             // 웹사이트에서 정해준 포스트 고유번호
        public string Url { get; }              // 하이퍼링크
        public string Title { get; }            // 제목
        public string Name { get; }             // 글쓴이
        public DateTime WriteDate { get; }      // 작성일
        public long ViewCount { get; }          // 조회
        public long Recommend { get; }          // 추천
        


        public CrawlResult(long postId, string url, string title, string name, DateTime writeDate, long viewCount, long recommend, int crawlType)
        {
            PostId = postId;
            Url = url;
            Title = title;
            Name = name;
            WriteDate = writeDate;
            ViewCount = viewCount;
            Recommend = recommend;
            CrawlType = crawlType;
        }

        public bool Equals(CrawlResult other)
        {
            return other.PostId == PostId && other.CrawlType == CrawlType;
        }
    }
}
