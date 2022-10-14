/*
 *
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

namespace RequestApi.Crawl
{
    public delegate void CrawlFailed(CrawlTask task);
    public delegate void CrawlSuccess(CrawlTask crawl, List<CrawlResult> crawlResult);
    public delegate void CrawlMatched(CrawlTask crawl, CrawlResult matchedResult);

    public class CrawlTaskManager
    {
        
        public bool Running { get; private set; }
        public bool HasTask
        {
            get
            {
                lock (this)
                {
                    return _crawlList.Count > 0;
                }
            }
        }

        public int[] CrawlDelay { get; }    // 각 쓰레드마다 하나의 원소를 접근해서 사용하므로 락은 필요 없음

        // 게시글 매칭 판정 시간 => 30분
        public static TimeSpan MatchingRange => new TimeSpan(0, 30, 0);

        // 매칭 판정된 게시글을 얼마나 기록할지 => 1일
        public static TimeSpan ExpiredDuration => new TimeSpan(1, 0, 0, 0);


        private List<CrawlTask> _crawlList;
        private List<MatchedCrawlResult> _completedResults;

        private int _delay;
        private Timer _workerTimer;
        private CrawlTaskWorker[] _taskWorkers;
        private AutoResetEvent[] _taskSignals;      
        private AutoResetEvent[] _taskWaitors;

        public CrawlFailed OnCrawlFailed;
        public CrawlSuccess OnCrawlSuccess;
        public CrawlMatched OnCrawlMatched;

        private static readonly string s_configDirName = "config";
        private static readonly string s_taskFileName = "tasks.json";             // 현재 진행중인 작업 저장용
        private static readonly string s_completeFileName = "today.json";         // 중복 검출 방지를 위한 목록 (저장 기간 1일)

        public CrawlTaskManager(int delay)
        {
            Running = false;
            CrawlDelay = new int[CrawlType.Max];
            CrawlDelay[CrawlType.FMKorea] = 100;
            CrawlDelay[CrawlType.DCInside] = 100;

            _delay = delay;
            _crawlList = new List<CrawlTask>();
            _completedResults = new List<MatchedCrawlResult>();

            _taskWorkers = new CrawlTaskWorker[CrawlType.Max];
            _taskSignals = new AutoResetEvent[CrawlType.Max];
            _taskSignals[CrawlType.FMKorea] = new AutoResetEvent(false);
            _taskSignals[CrawlType.DCInside] = new AutoResetEvent(false);

            _taskWaitors = new AutoResetEvent[CrawlType.Max];
            _taskWaitors[CrawlType.FMKorea] = new AutoResetEvent(false);
            _taskWaitors[CrawlType.DCInside] = new AutoResetEvent(false);

            _taskWorkers[CrawlType.FMKorea] = new CrawlTaskWorker(
                CrawlDelay[CrawlType.FMKorea],
                _taskWaitors[CrawlType.FMKorea],
                _taskSignals[CrawlType.FMKorea],
                CrawlType.FMKorea
            );

            _taskWorkers[CrawlType.DCInside] = new CrawlTaskWorker(
                CrawlDelay[CrawlType.DCInside],
                _taskWaitors[CrawlType.DCInside],
                _taskSignals[CrawlType.DCInside],
                CrawlType.DCInside
            );

        }

        public void Start()
        {
            if (Running)
                return;

            for (int i = 0; i < CrawlType.Max; i++)
                _taskWorkers[i].Start();

            Running = true;
            _workerTimer = new Timer(OnTick, this, 0, _delay);
            
        }

        public void Stop()
        {
            if (!Running)
                return;

            for (int i = 0; i < CrawlType.Max; i++)
                _taskWorkers[i].Stop();

            Running = false;
            _workerTimer?.Dispose();
        }

        public bool TryLoadCompleteFile()
        {
            if (Running)
                return false;

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
                    long viewCount = long.Parse(match["VviewCount"].ToString());
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
                
                foreach (JObject crawlObj in crawlArray)
                {
                    int crawlType = int.Parse(crawlObj["crawlType"].ToString());
                    int page = int.Parse(crawlObj["page"].ToString());
                    int boardType = int.Parse(crawlObj["boardType"].ToString());
                    int matchRule = int.Parse(crawlObj["matchRule"].ToString());
                    int matchType = int.Parse(crawlObj["matchType"].ToString());
                    string matchString = crawlObj["matchString"].ToString();
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
                    _crawlList.Add(new CrawlTask(crawl, matchString, (CrawlMatchRule)matchRule, (CrawlMatchType)matchType, this));
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
            if (Running) throw new Exception("먼저 정지해주세요.");

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

                    crawlObj.Add("crawlType", (int)crawlTask.CrawlType);
                    crawlObj.Add("matchString", crawlTask.MatchString);
                    crawlObj.Add("matchRule", (int)crawlTask.MatchRule);
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

        public void RegisterDCCrawl(string matchString, CrawlMatchRule matchRule, CrawlMatchType matchType, DCBoardType boardType = DCBoardType.전체글, int page = 1)
        {
            AbstractCrawl crawl = new DCInsideCrawl(boardType, page);
            CrawlTask crawlTask = new CrawlTask(crawl, matchString, matchRule, matchType, this);
            
            lock (this)
                _crawlList.Add(crawlTask);

            SaveTaskFile();
        }

        public void RegisterFMCrawl(string matchString, CrawlMatchRule matchRule, CrawlMatchType matchType, FMBoardType boardType = FMBoardType.전체, int page = 1)
        {
            AbstractCrawl crawl = new FMKoreaCrawl(boardType, page);
            CrawlTask crawlTask = new CrawlTask(crawl, matchString, matchRule, matchType, this);
            
            lock (this)
                _crawlList.Add(crawlTask);

            SaveTaskFile();
        }

        public void RegisterFMSearchCrawl(string matchString, CrawlMatchRule matchRule, CrawlMatchType matchType, FMSearchOption searchOption, string searchContent, FMBoardType boardType = FMBoardType.전체, int page = 1)
        {
            AbstractCrawl crawl = new FMKoreaCrawl(searchOption, searchContent, boardType, page);
            CrawlTask crawlTask = new CrawlTask(crawl, matchString, matchRule, matchType, this);

            lock (this) 
                _crawlList.Add(crawlTask);
            SaveTaskFile();
        }

        public void Unregister(CrawlTask crawl)
        {
            lock (this) 
                _crawlList.Remove(crawl);

            SaveTaskFile();
        }

        public void Clear()
        {
            lock (this)
                _crawlList.Clear();
            SaveTaskFile();
        }

        private void OnTick(object state)
        {
            Debug.WriteLine($"Tick Begin");

            lock (this)
            {
                foreach (var crawlTask in _crawlList)
                    _taskWorkers[crawlTask.CrawlType].Add(crawlTask);
            }

            foreach (var worker in _taskWorkers)
                worker.Waitor.Set();

            WaitHandle.WaitAll(_taskSignals);
            Debug.WriteLine($"Tick Complete");
        }


    }
}
