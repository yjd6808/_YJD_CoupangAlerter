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
    public static class WaitHandleExtension
    {
        public static void ReleaseAcquireWaitOne(this WaitHandle waitHandle, object relseqLock, int timeout = -1)
        {
            if (!Monitor.IsEntered(relseqLock))
                throw new Exception("락이 안되어있는데요?");

            Monitor.Exit(relseqLock);       // Release 수행
            waitHandle.WaitOne(timeout);    // 기본적으로 무제한 대기 (https://learn.microsoft.com/en-us/dotnet/api/system.threading.waithandle.waitone?view=net-7.0)
            Monitor.Enter(relseqLock);      // 다시 Acquire 수행
        }

    }
}
