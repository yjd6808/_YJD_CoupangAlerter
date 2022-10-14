using RequestApi.Crawl.Result;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RequestApi.Crawl
{
    public enum CrawlMatchType
    {
        Title,
        NickName,
    }

    public enum CrawlMatchRule
    {
        Contain,
        Exact
    }





    public class CrawlTask
    {
        public AbstractCrawl Crawl { get; }
        public string MatchString { get; }
        public int CrawlType => Crawl.Type;
        public CrawlMatchRule MatchRule { get; }
        public CrawlMatchType MatchType { get; }
        private CrawlTaskManager Manager { get; }


        public CrawlTask(AbstractCrawl crawl, string matchString, CrawlMatchRule matchRule, CrawlMatchType matchType, CrawlTaskManager manager)
        {
            Crawl = crawl;
            MatchString = matchString;
            MatchRule = matchRule;
            MatchType = matchType;
            Manager = manager;  
        }

        public void Do()
        {
            List<CrawlResult> crawlResult = Crawl.TryCrawl();

            if (crawlResult == null)
            {
                Manager.OnCrawlFailed?.Invoke(this);
                return;
            }

            Manager.OnCrawlSuccess?.Invoke(this, crawlResult);

            foreach (var result in crawlResult)
            {
                if (IsMatchedResult(result))
                    Manager.OnCrawlMatched?.Invoke(this, result);
            }

            Thread.Sleep(100);
        }

        private bool IsMatchedResult(CrawlResult result)
        {
            string matchee = string.Empty;

            switch (MatchType)
            {
                case CrawlMatchType.Title:
                    matchee = result.Title;
                    break;
                case CrawlMatchType.NickName:
                    matchee = result.Name;
                    break;
            }

            switch (MatchRule)
            {
                case CrawlMatchRule.Contain:
                    return matchee.Contains(MatchString);
                case CrawlMatchRule.Exact:
                    return matchee == MatchString;
                default:
                    return false;
            }
        }
    }
}
