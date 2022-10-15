/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RequestApi.Utils
{
    public static class ThreadExtension
    {
        public static void ReleaseAcquireSleep(object relseqLock, int miliSecond)
        {
            if (!Monitor.IsEntered(relseqLock))
                throw new Exception("락이 안되어있는데요?");

            Monitor.Exit(relseqLock);       // Release 수행
            Thread.Sleep(miliSecond);          
            Monitor.Enter(relseqLock);      // 다시 Acquire 수행
        }

    }
}
