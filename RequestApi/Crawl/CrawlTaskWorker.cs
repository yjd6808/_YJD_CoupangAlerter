/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RequestApi.Utils;

namespace RequestApi.Crawl
{
    // 크롤 타입(에펨코리아, 디시인사이드)별로 일정 주기마다 처리하기위한 워커쓰레드
    // 같은 웹 주소에 연속해서 빠르게 크롤링을 시도할 경우 웹에서 디도스로 인식하고 차단할 수 있으므로..
    public class CrawlTaskWorker
    {
        private Thread _workerThread;

        private readonly int _crawlType;
        
        private volatile bool _running;

        private int _delay;

        public AutoResetEvent Waitor { get; }
        public AutoResetEvent Signal { get; }


        private readonly LinkedList<CrawlTask> _taskQueue;

        public CrawlTaskWorker(int delay, AutoResetEvent waitor, AutoResetEvent signal, int crawlType)
        {
            _taskQueue = new LinkedList<CrawlTask>();
            _running = false;
            _delay = delay;
            _crawlType = crawlType;

            Waitor = waitor;
            Signal = signal;
        }

        public void Start()
        {
            if (_running) throw new Exception("이미 실행중입니다.");
            _running = true;
            _workerThread = new Thread(WorkerThread);
            _workerThread.IsBackground = true;
            _workerThread.Start();
        }

        public void SetDelay(int delay)
        {
            if (_running) throw new Exception("실행중에는 설정 ㄴ");
            _delay = delay;
        }

        public void Stop()
        {
            if (!_running) throw new Exception("시작되지 않았습니다.");
            _running = false;
            Waitor.Set();               // 쓰레드 쉬는 장소 (1)에서 자고 이쓴 상태를 강제로 깨우기 위함
            lock (this)                 // 쓰레드 쉬는 장소 (2)에서 자고 있는 상태를 강제로 깨우기 위함
                Monitor.Pulse(this);
            _workerThread.Join();
        }

        public void Add(CrawlTask task)
        {
            lock (this)
            {
                _taskQueue.AddLast(task);
            }
        }

        public void Clear()
        {
            lock (this)
            {
                _taskQueue.Clear();
            }
        }

        public void Remove(CrawlTask task)
        {
            lock (this)
            {
                _taskQueue.Remove(task);
            }
        }

        // ===========================================
        private void WorkerThread()
        {
            while (_running)
            {
                Monitor.Enter(this);
                try
                {
                    // CrawlTaskManager의 OnTick() 함수가 실행완료되어서 _taskQueue에 크롤링 작업들이 모두 찬 경우
                    // CrawlTaskManager에서 _waitor로 시그널을 보내주도록 하였다.
                    // 쓰레드 쉬는 장소 (1)
                    Waitor.ReleaseAcquireWaitOne(this);

                    if (!_running)
                        return;

                    LinkedListNode<CrawlTask> cur = _taskQueue.First;

                    while (cur != null)
                    {
                        var temp = cur;

                        cur.Value.Do();
                        cur = cur.Next;

                        // 처리한 작업은 삭제해주자.
                        _taskQueue.Remove(temp);


                        // 크롤링 시간 후 대기하는 시간이 너무 길어질 수 있으므로 주기적으로 얼마나 잤는지 체크해준다.
                        // 쓰레드 쉬는 장소 (2)
                        int sleepTime = 0;
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        for (;;)
                        {
                            Monitor.Wait(this);
                            
                            if (!_running)
                                return;

                            stopwatch.Stop();
                            sleepTime += (int)stopwatch.Elapsed.TotalMilliseconds;
                            stopwatch.Reset();
                            stopwatch.Start();
                            if (sleepTime >= _delay)
                                break;
                        }
                    }
                }
                finally
                {
                    // 작업을 마치면 CrawlTaskManager로 시그널을 보내줘서 작업이 완료됬음을 알려준다.
                    Signal.Set();
                    Monitor.Exit(this);
                }
            }
        }
    }
}
