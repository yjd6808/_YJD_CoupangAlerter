using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl
{
    public static class CrawlType
    {
        public const int DCInside = 0;
        public const int FMKorea = 1;
        public const int Max = 2;

        public static string ToString(int crawlType)
        {
            switch (crawlType)
            {
                case DCInside: return "디시인사이드";
                case FMKorea: return "에펨코리아";
                default: throw new ArgumentException("잘못된 인자입니다.");
            }
        }
    }
}
