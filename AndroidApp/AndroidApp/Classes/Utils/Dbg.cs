/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 10/20/2022 4:42:28 AM
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AndroidApp.Classes.Utils
{
    public static class Dbg
    {
        public static void WrilteLine(object obj)
        {
#if DEBUG
            Console.WriteLine($"[ 윤정도 ][{string.Format("{0,5}", Thread.CurrentThread.ManagedThreadId)}] {obj}");

            // 아래처럼 줄일 수 있음 ㄷㄷ;
            // Console.WriteLine($"[ 윤정도 ][{$"{Thread.CurrentThread.ManagedThreadId,5}"}] {obj}");
            // Console.WriteLine($"[ 윤정도 ][{Thread.CurrentThread.ManagedThreadId,5}] {obj}");
#endif
        }
    }
}
