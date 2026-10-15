/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Crawl
{
    public enum WebCrawlMethod
    {
        AppWebRequest = 0,
        ExternalAppWebRequest = 1,
        // 열려 있는 Edge의 FM 탭을 UIA로 직접 읽는다 (Windows 전용).
        // 핸들러는 WindowsApp 시작 시 EdgeUiFetch.Fetch로 연결한다.
        EdgeDirectUIA = 2
    }
}
