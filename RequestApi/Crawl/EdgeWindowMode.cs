/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 * 
 * 크롤링 시 엣지 페이지를 어떤 형태로 열지 결정한다.
 * WebFetch.exe의 2번째 인자(tab|window|hidden)로 전달된다.
 * * * * * * * * * * * * *
 */

namespace RequestApi.Crawl
{
    public enum EdgeWindowMode
    {
        Tab,        // 기존 엣지 창의 새 탭
        Window,     // 새 엣지 창
        Hidden,     // 새 엣지 창 (최소화 상태로 열어 화면에 표시하지 않음)
    }
}
