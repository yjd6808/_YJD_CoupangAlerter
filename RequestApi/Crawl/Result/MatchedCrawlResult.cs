/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl.Result
{
    public class MatchedCrawlResult
    {
        public CrawlResult Result { get; }              // 매칭된 크롤링 결과
        public DateTime MatchedTime { get; }            // 매칭된 시간
        public bool IsSuppressed { get; set; }          // 첫 번째 크롤링 사이클 매칭(true시 시각/청각 알림 억제)

        public MatchedCrawlResult(CrawlResult result, DateTime matchedTime)
        {
            Result = result;
            MatchedTime = matchedTime;
        }
    }
}
