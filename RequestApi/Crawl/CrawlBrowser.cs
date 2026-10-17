/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 크롤링에 사용하는 브라우저를 나타낸다.
 * 실제 크롤링(WebFetch)은 포트만 받아 동작하므로
 * 이 값은 어떤 브라우저의 CDP 포트를 사용할지 결정하는 데 쓰인다.
 * * * * * * * * * * * * *
 */

namespace RequestApi.Crawl
{
    public enum CrawlBrowser
    {
        Edge = 0,
        Chrome = 1
    }
}
