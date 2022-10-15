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

        public MatchedCrawlResult(CrawlResult result, DateTime matchedTime)
        {
            Result = result;
            MatchedTime = matchedTime;
        }
    }
}
