using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RequestApi.Utils
{
    public static class WaitHandleExtension
    {
        public static void ReleaseAcquireWaitOne(this WaitHandle waitHandld, object relseqLock, int timeout = -1)
        {
            if (Monitor.IsEntered(relseqLock))
                throw new Exception("데드락 상태입니다.");

            Monitor.Exit(waitHandld);       // Release 수행
            waitHandld.WaitOne(timeout);    // 기본적으로 무제한 대기 (https://learn.microsoft.com/en-us/dotnet/api/system.threading.waithandle.waitone?view=net-7.0)
            Monitor.Enter(waitHandld);      // 다시 Acquire 수행
        }

    }
}
