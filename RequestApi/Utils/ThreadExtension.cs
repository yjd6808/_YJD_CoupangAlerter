using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RequestApi.Utils
{
    public static class ThreadExtension
    {
        public static void ReleaseAcquireSleep(object relseqLock, int miliSecond = -1)
        {
            if (Monitor.IsEntered(relseqLock))
                throw new Exception("데드락 상태입니다.");

            Monitor.Exit(relseqLock);       // Release 수행
            Thread.Sleep(miliSecond);          
            Monitor.Enter(relseqLock);      // 다시 Acquire 수행
        }

    }
}
