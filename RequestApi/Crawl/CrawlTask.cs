/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using RequestApi.Crawl.Result;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace RequestApi.Crawl
{
    public enum CrawlMatchType
    {
        Title,
        NickName,
    }

    public enum CrawlStringMatchRule
    {
        Contain,
        Exact
    }

    public class CrawlTask
    {
        public AbstractCrawl Crawl { get; }
        public long UID { get; private set; }
        public string TaskName { get; }
        public string MatchContent { get; }
        public int CrawlType => Crawl.Type;
        public bool IsBlocked => _manager.BlockedCrawl[CrawlType] || _isBlocked;
        public CrawlStringMatchRule StringMatchRule { get; }
        public CrawlMatchType MatchType { get; }
        public int MatchingRangeMinute { get; }         // 몇분 내의 게시글을 유효 게시글로 판정할 지
                                                        // 예를들어서 DC 인사이드 개념글을 크롤링하는데 글을 쓴지 1시간이 지난 후 개념글로 전환되면
                                                        // 이 시간을 너무 짧게 잡아버리면 원하는 게시글을 잡아내지 못하게 될 수 있다.

        public static long s_uidSeq = 0;                // 작업별 고유 번호. - crawls.json에 이 고유값은 계속 기록하도록 한다. 껏다 켜도 유지되도록
        

        private readonly CrawlTaskManager _manager;
        private volatile bool _isBlocked;   
        
        public CrawlTask(string traskName, AbstractCrawl crawl, string matchContent, CrawlStringMatchRule stringMatchRule, CrawlMatchType matchType, int matchingRangeMinute, CrawlTaskManager manager, long uid = -1)
        {
            UID = uid <= 0 ? Interlocked.Increment(ref s_uidSeq) : uid; // 프로그램에서 생성하지 않고 초기 crawls.json에서 task 로딩시 고유 UID 값을 넣어줄 수 있도록 함
            TaskName = traskName;
            Crawl = crawl;
            MatchContent = matchContent;
            StringMatchRule = stringMatchRule;
            MatchType = matchType;
            MatchingRangeMinute = matchingRangeMinute;

            _manager = manager;
        }

        public void Block()
        {
            _isBlocked = true;
        }

        public void Do()
        {
            if (IsBlocked) 
                return;

            _manager.Stat.RequestCount++;
            _manager.OnCrawlRequest?.Invoke(this);
            List<CrawlResult> crawlResult = Crawl.TryCrawl(out string errorMessage);
            DateTime matchedTime = DateTime.Now;
            

            if (crawlResult == null)
            {
                _manager.Stat.RequestFailedCount++;
                _manager.OnCrawlFailed?.Invoke(this, errorMessage);
                return;
            }

            _manager.Stat.RequestSuccessCount++;
            _manager.OnCrawlSuccess?.Invoke(this, crawlResult);

            foreach (var result in crawlResult)
            {
                if (IsMatchedResult(result, matchedTime))
                {
                    _manager.Stat.RequestMatchedCount++;
                    _manager.OnCrawlMatched?.Invoke(this, new MatchedCrawlResult(result, matchedTime));
                }
            }

        }

        private bool IsMatchedResult(CrawlResult result, DateTime matchedTime)
        {
            string matchee = string.Empty;

            if (matchedTime >= result.WriteDate + TimeSpan.FromMinutes(MatchingRangeMinute))
                return false;

            if (_manager.CheckAlreadyMatchedResult(result))
                return false;

            switch (MatchType)
            {
                case CrawlMatchType.Title:
                    matchee = result.Title;
                    break;
                case CrawlMatchType.NickName:
                    matchee = result.Name;
                    break;
            }

            switch (StringMatchRule)
            {
                case CrawlStringMatchRule.Contain:
                    return matchee.Contains(MatchContent);
                case CrawlStringMatchRule.Exact:
                    return matchee == MatchContent;
                default:
                    return false;
            }
        }

        public bool Equals(CrawlTask other)
        {
            if (this == other)
                return true;

            return other.UID == this.UID;
        }

        public CrawlTask Clone()
        {
            return new CrawlTask(TaskName, Crawl.Clone(), MatchContent, StringMatchRule, MatchType, MatchingRangeMinute, _manager);
        }
    }
}
