/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

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

        public static string ToStringInitial(int crawlType)
        {
            switch (crawlType)
            {
                case DCInside: return "DC";
                case FMKorea: return "FM";
                default: throw new ArgumentException("잘못된 인자입니다.");
            }
        }


        public static int ToFlag(int crawlType)
        {
            switch (crawlType)
            {
                case DCInside: return 1;
                case FMKorea: return 2;
                // case FMKorea: return 4;
                default: throw new ArgumentException("잘못된 인자입니다.");
            }
        }
    }
}
