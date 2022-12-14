/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RequestApi.Crawl.Result;
using RequestApi.Utils;

#pragma warning disable 0168        // local variable not used

namespace RequestApi.Crawl
{
    public delegate void CrawlRequest(CrawlTask task);
    public delegate void CrawlFailed(CrawlTask task, string errorMessage);
    public delegate void CrawlSuccess(CrawlTask crawl, List<CrawlResult> crawlResult);
    public delegate void CrawlMatched(CrawlTask crawl, MatchedCrawlResult matchedResult);

    public class CrawlTaskManager
    {
        public bool Running => _running;
        public bool HasTask
        {
            get
            {
                lock (this)
                {
                    int runnableTaskCount = 0;
                    foreach (var crawlTask in _crawlList)
                    {
                        if (_blockedCrawl[crawlTask.CrawlType]) continue;
                        runnableTaskCount++;
                    }
                    return runnableTaskCount > 0;
                }
            }
        }

        public int[] CrawlDelay => _crawlDelay;
        public bool[] BlockedCrawl => _blockedCrawl;
        public Statistics Stat { get; }



        // 매칭 판정된 게시글을 얼마나 기록할지 => 1일
        // 이걸 기록안하면 매칭된 게시글 결과 정보가 계속 누적되게 될 수 있으므로 주기적으로 확인해서 비워줄 필요가 있다.
        public static TimeSpan ExpiredDuration => new TimeSpan(1, 0, 0, 0);
        public const int DefaultMatchingRange = 1440;

        private Thread _workerThread;

        private readonly List<CrawlTask> _crawlList;
        private readonly List<MatchedCrawlResult> _completedResults;     // 매칭된 게시글 결과들. (매칭 될 때마다 기록을 해서 중복 검출이 되지 않도록 막아준다.)

        private readonly CrawlTaskWorker[] _taskWorkers;
        private readonly AutoResetEvent[] _taskWaitors;

        private volatile bool _running;
        private volatile int[] _crawlDelay;
        private volatile bool[] _blockedCrawl;

        public CrawlRequest OnCrawlRequest;
        public CrawlFailed OnCrawlFailed;
        public CrawlSuccess OnCrawlSuccess;
        public CrawlMatched OnCrawlMatched;

        private string s_configDirName = null;
        private readonly string s_taskFileName = "tasks.json";             // 현재 진행중인 작업 저장용
        private readonly string s_completeFileName = "today.json";         // 중복 검출 방지를 위한 목록 (저장 기간 1일)

        public CrawlTaskManager()
        {
            Stat = new Statistics();

            _running = false;

            _blockedCrawl = new bool[CrawlType.Max];
            _blockedCrawl[CrawlType.FMKorea] = false;
            _blockedCrawl[CrawlType.DCInside] = false;

            _crawlDelay = new int[CrawlType.Max];
            _crawlDelay[CrawlType.DCInside] = 180000;
            _crawlDelay[CrawlType.FMKorea] = 300000;
            _crawlList = new List<CrawlTask>();
            _completedResults = new List<MatchedCrawlResult>();

            _taskWorkers = new CrawlTaskWorker[CrawlType.Max];

            _taskWaitors = new AutoResetEvent[CrawlType.Max];
            _taskWaitors[CrawlType.FMKorea] = new AutoResetEvent(false);
            _taskWaitors[CrawlType.DCInside] = new AutoResetEvent(false);

            _taskWorkers[CrawlType.FMKorea] = new CrawlTaskWorker(
                CrawlDelay[CrawlType.FMKorea],
                _taskWaitors[CrawlType.FMKorea],
                CrawlType.FMKorea
            );

            _taskWorkers[CrawlType.DCInside] = new CrawlTaskWorker(
                CrawlDelay[CrawlType.DCInside],
                _taskWaitors[CrawlType.DCInside],
                CrawlType.DCInside
            );

            OnCrawlMatched += (crawl, result) =>
            {
                lock (this)
                    _completedResults.Add(result);
            };
        }

        public void SetConfigDirectory(string path)
        {
            s_configDirName = path;
        }

        public string GetConfigDirectory()
        {
            return s_configDirName;
        }

        public void Start()
        {
            if (s_configDirName == null)
                throw new Exception("설정 파일 저장 위치가 설정되지 않았습니다.");

            if (Running)
                return;

            for (int i = 0; i < CrawlType.Max; i++)
            {
                _taskWaitors[i].Reset();
                _taskWorkers[i].Start();
            }

            _running = true;
            _workerThread = new Thread(WorkerThread);
            _workerThread.Start();
        }

        public void Stop()
        {
            if (!Running)
                return;

            _running = false;

            for (int i = 0; i < CrawlType.Max; i++)
            {
                _taskWorkers[i].Clear();
                _taskWorkers[i].Stop();
            }

            /*

            _running = false; // ---> 잘못된 코드
             
            _running = false는 무조건 맨 위에서 실행해줘야한다. 이렇게 안해주면 데드락 걸림
            _taskWorker로 Stop을 호춣해주면 _taskWorker의 쓰레드들이 현재 진행중인 작업이 완료되면 종료된다.
            이때 Manager의 워커쓰레드의 WaitHandle.WaitAll() 함수로 블록된 쓰레드도 동시에 깨어나는데
            만약 _running = false를 _taskWorker[i].Stop() 반복문 구문 이후에 처리해주게 되면 WaitAll() 함수 이후에도 _running의 상태가 
            true일 수도 있고 false일 수도 있는 불확실한 상태가 되기 때문이다.

            */

            _workerThread.Join();
            _workerThread = null;

            SaveCompleteFile();
        }

        public bool TryLoadCompleteFile()
        {
            if (Running)
                return false;

            _completedResults.Clear();

            try
            {
                string loadDir = Path.Combine(Environment.CurrentDirectory, s_configDirName);
                string loadCompletFilePath = Path.Combine(loadDir, s_completeFileName);

                if (!File.Exists(loadCompletFilePath))
                    return false;

                JObject root = JObject.Parse(File.ReadAllText(loadCompletFilePath));
                JArray matchArray = root["matches"] as JArray;

                foreach (JObject match in matchArray)
                {
                    int crawlType = int.Parse(match["crawlType"].ToString());
                    long postId = long.Parse(match["postId"].ToString());
                    DateTime matchedTime = DateTime.Parse(match["matchedTime"].ToString());
                    string url = match["url"].ToString();
                    string title = match["title"].ToString();
                    string name = match["name"].ToString();
                    DateTime writeDate = DateTime.Parse(match["writeDate"].ToString());
                    long viewCount = long.Parse(match["viewCount"].ToString());
                    long recommendCount = long.Parse(match["recommendCount"].ToString());

                    CrawlResult crawlResult = new CrawlResult(postId, url, title, name, writeDate, viewCount, recommendCount, crawlType);
                    _completedResults.Add(new MatchedCrawlResult(crawlResult, matchedTime));
                }

                return true;
            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
                return false;
#endif
            }
        }


        public bool TryLoadTaskFile()
        {
            if (Running)
                return false;

            _crawlList.Clear();

            try
            {
                string loadDir = Path.Combine(Environment.CurrentDirectory, s_configDirName);
                string loadTaskFilePath = Path.Combine(loadDir, s_taskFileName);

                if (!File.Exists(loadTaskFilePath))
                    return false;

                JObject root = JObject.Parse(File.ReadAllText(loadTaskFilePath));
                JArray crawlArray = root["crawls"] as JArray;

                CrawlDelay[CrawlType.DCInside] = int.Parse(root["dcCrawlDelay"].ToString());
                CrawlDelay[CrawlType.FMKorea] = int.Parse(root["fmCrawlDelay"].ToString());
                _blockedCrawl[CrawlType.DCInside] = bool.Parse(root["dcCrawlBlocked"].ToString());
                _blockedCrawl[CrawlType.FMKorea] = bool.Parse(root["fmCrawlBlocked"].ToString());
                CrawlTask.s_uidSeq = long.Parse(root["crawlTaskUidSeq"].ToString());

                _taskWorkers[CrawlType.DCInside].SetDelay(CrawlDelay[CrawlType.DCInside]);
                _taskWorkers[CrawlType.FMKorea].SetDelay(CrawlDelay[CrawlType.FMKorea]);

                foreach (JObject crawlObj in crawlArray)
                {
                    int crawlType = int.Parse(crawlObj["crawlType"].ToString());
                    long uid = long.Parse(crawlObj["uid"].ToString());
                    string taskName = crawlObj["taskName"].ToString();
                    int page = int.Parse(crawlObj["page"].ToString());
                    int boardType = int.Parse(crawlObj["boardType"].ToString());
                    int matchRule = int.Parse(crawlObj["stringMatchRule"].ToString());
                    int matchType = int.Parse(crawlObj["matchType"].ToString());
                    string matchString = crawlObj["matchString"].ToString();
                    int matchingRangeMinute = int.Parse(crawlObj["matchingRangeMinute"].ToString());
                    AbstractCrawl crawl = null;
                    switch (crawlType)
                    {
                        case CrawlType.DCInside:
                            crawl = new DCInsideCrawl((DCBoardType)boardType, page);
                            break;
                        case CrawlType.FMKorea:
                            page = int.Parse(crawlObj["page"].ToString());
                            boardType = int.Parse(crawlObj["boardType"].ToString());
                            int searchOption = int.Parse(crawlObj["searchOption"].ToString());
                            string searchContent = crawlObj["searchContent"].ToString();
                            crawl = new FMKoreaCrawl((FMSearchOption)searchOption, searchContent, (FMBoardType)boardType, page);
                            break;
                    }

                    if (crawl == null) throw new Exception("파싱 제대로 안됨");

                    var task = new CrawlTask(
                        taskName,
                        crawl, 
                        matchString, 
                        (CrawlStringMatchRule)matchRule,
                        (CrawlMatchType)matchType, 
                        matchingRangeMinute, 
                        this,
                        uid
                    );
                    _crawlList.Add(task);
                }


                return true;
            }
            catch (Exception e)
            {
#if DEBUG
                throw;
#else
                return false;
#endif
            }
        }

        public void SaveTaskFile()
        {
            string saveDir = Path.Combine(Environment.CurrentDirectory, s_configDirName);
            string saveFilePath = Path.Combine(saveDir, s_taskFileName);

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            JObject root = new JObject();
            JArray crawlArray = new JArray();

            lock (this)
            {
                _crawlList.ForEach(crawlTask =>
                {
                    JObject crawlObj = new JObject();

                    crawlObj.Add("uid", crawlTask.UID);
                    crawlObj.Add("taskName", crawlTask.TaskName);
                    crawlObj.Add("crawlType", crawlTask.CrawlType);
                    crawlObj.Add("matchString", crawlTask.MatchContent);
                    crawlObj.Add("stringMatchRule", (int)crawlTask.StringMatchRule);
                    crawlObj.Add("matchingRangeMinute", crawlTask.MatchingRangeMinute);
                    crawlObj.Add("matchType", (int)crawlTask.MatchType);

                    switch (crawlTask.CrawlType)
                    {
                        case CrawlType.DCInside:
                            var dcCrawl = crawlTask.Crawl.To<DCInsideCrawl>();
                            crawlObj.Add("page", dcCrawl.Page);
                            crawlObj.Add("boardType", (int)dcCrawl.BoardType);
                            break;
                        case CrawlType.FMKorea:
                            var fmCrawl = crawlTask.Crawl.To<FMKoreaCrawl>();
                            crawlObj.Add("page", fmCrawl.Page);
                            crawlObj.Add("boardType", (int)fmCrawl.BoardType);
                            crawlObj.Add("searchOption", (int)fmCrawl.SearchOption);
                            crawlObj.Add("searchContent", fmCrawl.SearchContent);
                            break;
                    }
                        
                    crawlArray.Add(crawlObj);
                });
            }

            root.Add("crawls", crawlArray);
            root.Add("dcCrawlDelay", CrawlDelay[CrawlType.DCInside]);
            root.Add("fmCrawlDelay", CrawlDelay[CrawlType.FMKorea]);
            root.Add("dcCrawlBlocked", _blockedCrawl[CrawlType.DCInside]);
            root.Add("fmCrawlBlocked", _blockedCrawl[CrawlType.FMKorea]);
            root.Add("crawlTaskUidSeq", Interlocked.Read(ref CrawlTask.s_uidSeq));

            File.WriteAllText(saveFilePath, root.ToString());
        }

        public void SaveCompleteFile() 
        {
            string saveDir = Path.Combine(Environment.CurrentDirectory, s_configDirName);
            string saveCompletFilePath = Path.Combine(saveDir, s_completeFileName);

            if (!Directory.Exists(saveDir))
                Directory.CreateDirectory(saveDir);

            JObject root = new JObject();
            JArray matchArray = new JArray();

            lock (this)
            {
                _completedResults.ForEach(matchResult =>
                {
                    if (DateTime.Now - matchResult.MatchedTime >= ExpiredDuration)
                        return;

                    CrawlResult result = matchResult.Result;
                    JObject matchObj = new JObject();
                    matchObj.Add("crawlType", result.CrawlType);
                    matchObj.Add("postId", result.PostId);
                    matchObj.Add("matchedTime", matchResult.MatchedTime);
                    matchObj.Add("url", result.Url);
                    matchObj.Add("title", result.Title);
                    matchObj.Add("name", result.Name);
                    matchObj.Add("writeDate", result.WriteDate);
                    matchObj.Add("viewCount", result.ViewCount);
                    matchObj.Add("recommendCount", result.Recommend);
                    matchArray.Add(matchObj);
                });
            }
            root.Add("matches", matchArray);
            File.WriteAllText(saveCompletFilePath, root.ToString());
        }

        public void ForEach(Action<CrawlTask> action)
        {
            lock (this)
            {
                foreach (var crawlTask in _crawlList)
                    action(crawlTask);
            }
        }

        public void ApplyDelay()
        {
            _taskWorkers[CrawlType.FMKorea].SetDelay(CrawlDelay[CrawlType.FMKorea]);
            _taskWorkers[CrawlType.DCInside].SetDelay(CrawlDelay[CrawlType.DCInside]);
        }

        
        // 빌더 패턴 고려할 것
        public void RegisterDCCrawl(string taskName, string matchContent, CrawlStringMatchRule stringMatchRule, CrawlMatchType matchType, int matchingRangeMinute = DefaultMatchingRange, DCBoardType boardType = DCBoardType.전체글, int page = 1)
        {
            AbstractCrawl crawl = new DCInsideCrawl(boardType, page);
            CrawlTask crawlTask = new CrawlTask(taskName, crawl, matchContent, stringMatchRule, matchType, matchingRangeMinute, this);
            
            lock (this)
                _crawlList.Add(crawlTask);

            SaveTaskFile();
        }

        public void RegisterFMCrawl(string taskName, string matchContent, CrawlStringMatchRule stringMatchRule, CrawlMatchType matchType, int matchingRangeMinute, FMSearchOption searchOption, string searchContent, FMBoardType boardType = FMBoardType.전체, int page = 1)
        {
            AbstractCrawl crawl = new FMKoreaCrawl(searchOption, searchContent, boardType, page);
            CrawlTask crawlTask = new CrawlTask(taskName, crawl, matchContent, stringMatchRule, matchType, matchingRangeMinute, this);

            lock (this) 
                _crawlList.Add(crawlTask);
            SaveTaskFile();
        }

        // 수정하고자하는 CrawlTask의 내용을 변경하는 행위 자체가 CrawlTask에서의 락 기능 구현을 강제해버리기 때문에 Idle 상태에서의 큰 성능저하가 발생한다.
        // 따라서 수정하고자하는 CrawlTask의 위치를 찾아낸 후 새로 생성 해서 동일한 위치에 삽입해주는 방식으로 구현하도록 한다.
        public void ModifyDCCrawl(CrawlTask modifyTask, string taskName, string matchContent, CrawlStringMatchRule stringMatchRule, CrawlMatchType matchType, int matchingRangeMinute = DefaultMatchingRange, DCBoardType boardType = DCBoardType.전체글, int page = 1)
        {
            AbstractCrawl crawl = new DCInsideCrawl(boardType, page);
            CrawlTask crawlTask = new CrawlTask(taskName, crawl, matchContent, stringMatchRule, matchType, matchingRangeMinute, this);

            
            lock (this)
            {
                int findIdx = _crawlList.FindIndex(0, _crawlList.Count, (x) => x == modifyTask);
                if (findIdx == -1) throw new Exception("수정하고자하는 작업을 찾지 못했습니다.");

                modifyTask.Block();     // 작업 수행이 안되도록 막아준다.                
                _crawlList.RemoveAt(findIdx);
                _crawlList.Insert(findIdx, crawlTask);
            }
                
            SaveTaskFile();
        }

        public void ModifyFMCrawl(CrawlTask modifyTask, string taskName, string matchContent, CrawlStringMatchRule stringMatchRule, CrawlMatchType matchType, int matchingRangeMinute, FMSearchOption searchOption, string searchContent, FMBoardType boardType = FMBoardType.전체, int page = 1)
        {
            AbstractCrawl crawl = new FMKoreaCrawl(searchOption, searchContent, boardType, page);
            CrawlTask crawlTask = new CrawlTask(taskName, crawl, matchContent, stringMatchRule, matchType, matchingRangeMinute, this);

            lock (this)
            {
                int findIdx = _crawlList.FindIndex(0, _crawlList.Count, (x) => x == modifyTask);
                if (findIdx == -1) throw new Exception("수정하고자하는 작업을 찾지 못했습니다.");

                modifyTask.Block();     // 작업 수행이 안되도록 막아준다.                
                _crawlList.RemoveAt(findIdx);
                _crawlList.Insert(findIdx, crawlTask);
            }

            SaveTaskFile();
        }


        public void Unregister(CrawlTask crawl)
        {
            lock (this) 
                _crawlList.Remove(crawl);

            SaveTaskFile();
        }

        public void ClearTasks()
        {
            lock (this)
                _crawlList.Clear();
            SaveTaskFile();
        }

        public void ClearCompletes()
        {
            lock (this)
                _completedResults.Clear();
            SaveCompleteFile();
        }


        // 코드가 너무 보기 안좋다.
        // 근데 로직을 아예 다 지우고 새로 작성하는거 아닌 이상 어쩔수가 없다.
        private async void WorkerThread(object state)
        {
            // 작업가능한 워커쓰레드별 상태를 나타내는 변수이다.
            bool[] readyToWork = new bool[CrawlType.Max];

            // 수행가능한 작업이 아예 없는 경우
            bool[] noJob = new bool[CrawlType.Max];
            
            BEGIN: 
            while (_running)
            {

                for (int i = 0; i < CrawlType.Max; i++)
                    readyToWork[i] = _taskWorkers[i].Empty();

                lock (this)
                {
                    // 기간이 지난 이미 매칭되었던 결과물들은 주기적으로 정리해주자.
                    _completedResults.RemoveAll(result => DateTime.Now - result.MatchedTime >= ExpiredDuration);

                    // 작업을 분배해줘야하는데 이미 작업을 진행중인 워커 쓰레드(readyToWork가 true)는 분배해주면 안된다.
                    foreach (var crawlTask in _crawlList)
                    {
                        if (_blockedCrawl[crawlTask.CrawlType]) continue;
                        if (!readyToWork[crawlTask.CrawlType]) continue;
                        _taskWorkers[crawlTask.CrawlType].Add(crawlTask);
                    }
                }

                for (int i = 0; i < CrawlType.Max; i++)
                {
                    // 작업을 시작시켜준다.
                    if (readyToWork[i])
                        _taskWorkers[i].Waitor.Set();

                    // 수행가능한 작업이 아예 엾는 경우
                    noJob[i] = _taskWorkers[i].Empty();
                }


                // 쓰레드 쉬는 장소(2)에서 블로킹 상태에 있는 쓰레드들을 일정시간마다 깨워서 시간을 체크해준다.
                // 하나의 워커 쓰레드라도 작업을 모두 완료하면 나간다.
                while (_running)
                {
                    for (int i = 0; i < CrawlType.Max; i++)
                    {
                        // 이미 Signal 핸들이 켜진 녀석은 채크할 필요가 없다.
                        var worker = _taskWorkers[i];

                        lock (worker)
                            Monitor.Pulse(worker);

                        if (noJob[i]) continue;

                        await Task.Delay(10);

                        // 하나의 워커쓰레드라도 작업을 완료하면 올라간다.
                        if (worker.Empty())
                            goto BEGIN;
                    }
                }
            }


            Debug.WriteLine("[분배 쓰레드가 종료되었습니다.]");
        }

        public bool CheckAlreadyMatchedResult(CrawlResult result)
        {
            lock (this)
            {
                return _completedResults.Find(x => x.Result.Equals(result)) != null;
            }
        }

        public class Statistics
        {
            public long RequestCount
            {
                get => Interlocked.Read(ref _requestCount);
                set => Interlocked.Exchange(ref _requestCount, value);
            }

            public long RequestFailedCount
            {
                get => Interlocked.Read(ref _requestFailedCount);
                set => Interlocked.Exchange(ref _requestFailedCount, value);
            }

            public long RequestSuccessCount
            {
                get => Interlocked.Read(ref _requestSuccessCount);
                set => Interlocked.Exchange(ref _requestSuccessCount, value);
            }

            public long RequestMatchedCount
            {
                get => Interlocked.Read(ref _requestMatchedCount);
                set => Interlocked.Exchange(ref _requestMatchedCount, value);
            }


            private long _requestCount = 0L;
            private long _requestFailedCount = 0L;
            private long _requestSuccessCount = 0L;
            private long _requestMatchedCount = 0L;
        }
    }
}
