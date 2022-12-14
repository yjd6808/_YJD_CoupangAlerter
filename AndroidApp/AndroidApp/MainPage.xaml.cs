using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AndroidApp.Classes.Services.App;
using AndroidApp.Classes.Services.Network;
using AndroidApp.Classes.Services.Notification;
using AndroidApp.Classes.Services.State;
using AndroidApp.Classes.Utils;
using Java.Nio.FileNio;
using Java.Sql;
using RequestApi.Crawl;
using RequestApi.Crawl.Result;
using Xamarin.CommunityToolkit.UI.Views;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace AndroidApp
{
    public class Log
    {
        public string Time { get; set; }
        public string Name { get; set; }
        public Brush NameForeground { get; set; }
        public TextDecorations NameDecoration { get; set; }
        public string Content { get; set; }
        public Brush ContentForeground { get; set; }
        public TextDecorations ContentDecoration { get; set; }
        public string Url { get; set; }
    }

   

    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<Log> _logs = new ObservableCollection<Log>();
        private readonly ObservableCollection<CrawlTask>[] _crawlTaskList = new ObservableCollection<CrawlTask>[CrawlType.Max];
        private readonly INotificationManager _notificationManager;
        private readonly IAppInitializer _appInitializer;
        private readonly SolidColorBrush _disabledForegroundColor = new SolidColorBrush(Color.FromRgb(126, 126, 126));
        private readonly CrawlTaskManager _crawlTaskManager = new CrawlTaskManager();
        private readonly object _waitForPhoneWakeUp = new ();
        private volatile bool _isPhoneWakeUp = false;
        private bool _isDataNetworkActivated = false;
        private bool _isWifiNetworkActivated = false;
        private CrawlTask _selectedCrawlTask;
        


        public MainPage()
        {
            _crawlTaskManager.SetConfigDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "CoupangAlerter/config"));
            _crawlTaskManager.TryLoadTaskFile();
            _crawlTaskManager.TryLoadCompleteFile();
            _crawlTaskManager.OnCrawlRequest += OnCrawlRequest;
            _crawlTaskManager.OnCrawlSuccess += OnCrawlSuccess;
            _crawlTaskManager.OnCrawlFailed += OnCrawlFailed;
            _crawlTaskManager.OnCrawlMatched += OnCrawlMatched;

            _notificationManager = DependencyService.Get<INotificationManager>();
            _appInitializer = DependencyService.Get<IAppInitializer>();
            _appInitializer.SetValue(typeof(CrawlTaskManager), _crawlTaskManager);
            _crawlTaskList[CrawlType.DCInside] = new ObservableCollection<CrawlTask>();
            _crawlTaskList[CrawlType.FMKorea] = new ObservableCollection<CrawlTask>();

            InitializeComponent();
            InitializeDefaultUIStates();
            

            _livLog.ItemsSource = _logs;
            _livDCCrawlList.ItemsSource = _crawlTaskList[CrawlType.DCInside];
            _livFMCrawlList.ItemsSource = _crawlTaskList[CrawlType.FMKorea];



#if DEBUG
            if (!_crawlTaskManager.HasTask)
                _crawlTaskManager.RegisterDCCrawl("ㅇㅇ", "ㅇㅇ", CrawlStringMatchRule.Contain, CrawlMatchType.NickName);
#endif


            // 인터넷 상태 확인방법
            CheckNetworkState();

            // 인터넷 상태 변경감지
            Connectivity.ConnectivityChanged += (sender, args) => Device.BeginInvokeOnMainThread(CheckNetworkState);
        }

        private void InitializeDefaultUIStates()
        {
            _chkbFMCrawlSearchOptionEnable.IsChecked = false;
            _chkbFMCrawlSearchContentEnable.IsChecked = false;
            _chkbFMCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.FMKorea];
            _chkbDCCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.DCInside];
            _tbDCCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.DCInside].ToString();
            _tbFMCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.FMKorea].ToString();
            _tbcCrawl.SelectedIndex = 0;

            

            SelectDCCrawlTabItem();
            UpdateCrawlTaskList();

            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.TextColor = _disabledForegroundColor.Color;
        }

        private void CheckNetworkState()
        {
            var isSimulator = DeviceInfo.DeviceType == DeviceType.Virtual;

            if (isSimulator)
            {
                _isDataNetworkActivated = false;
                _isWifiNetworkActivated = true;
                return;
            }

            _isDataNetworkActivated = false;
            _isWifiNetworkActivated = false;

            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                if (DependencyService.Get<INetConnectivityService>().IsActivated(NetConnectivityType.Cellular))
                {
                    _isDataNetworkActivated = true;
                    AddNoticeLog("데이터 통신이 켜져있습니다.");
                }

                if (DependencyService.Get<INetConnectivityService>().IsActivated(NetConnectivityType.Wifi))
                    _isWifiNetworkActivated = true;

                AddNoticeLog($"인터넷 상태: 연결 중");
            }
            else
            {
                AddNoticeLog("인터넷 연결을 해주세요.");
            }
        }

        

        private void OnCrawlRequest(CrawlTask task)
        {
            /* 매칭될 때만 화면 밝아지도록 함 OnCrawlMatched()
            Monitor.Enter(_waitForPhoneWakeUp);
            WaitForWake(true);
            */
        }

        private void OnCrawlMatched(CrawlTask crawl, MatchedCrawlResult matchedresult)
        {
            lock (_waitForPhoneWakeUp)
            {
                _isPhoneWakeUp = false;
                WaitForWake(true);

                UpdateStatusBar();
                AddCrawlMatchedLog(matchedresult);

                _notificationManager.SendNotification($"[{matchedresult.MatchedTime:tt h:mm:ss}] {matchedresult.Result.Name}", matchedresult.Result.Title);

                WaitForWake(false);
            }
        }

        // WaitForWake는 무조건 다른 쓰레드에서 실행되어야함.
        private void WaitForWake(bool state)
        {
            Dispatcher.BeginInvokeOnMainThread(() =>
            {
                DependencyService.Get<IForegroundServiceController>().WakeLock(state);
                _isPhoneWakeUp = state;
            });

            // _isPhoneWakeUp이 state가 되야 반복문을 탈출
            while (_isPhoneWakeUp != state)
            {

            }
        }

        private void OnCrawlFailed(CrawlTask task, string errorMessage)
        {
            //WaitForWake(false);
            //Monitor.Exit(_waitForPhoneWakeUp);

            UpdateStatusBar();

            if (errorMessage != string.Empty)
                AddNoticeLog(errorMessage);
        }

        private void OnCrawlSuccess(CrawlTask crawl, List<CrawlResult> crawlresult)
        {
            //WaitForWake(false);
            //Monitor.Exit(_waitForPhoneWakeUp);

            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            Dispatcher.BeginInvokeOnMainThread(() => _tblStatusBar.Text =
                $"요청: {_crawlTaskManager.Stat.RequestCount} " +
                $"성공: {_crawlTaskManager.Stat.RequestSuccessCount} " +
                $"실패: {_crawlTaskManager.Stat.RequestFailedCount} " +
                $"매칭: {_crawlTaskManager.Stat.RequestMatchedCount}");
        }

        private void AddNoticeLog(string content, string url = "")
        {
            var log = new Log
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Name = "[공지]",
                NameForeground = Brush.Black,
                Content = content,
                ContentForeground = Brush.Black
            };

            _logs.Add(log);
            _livLog.ScrollTo(log, ScrollToPosition.End, true);
            Dbg.WrilteLine(content);
        }

        private void AddCrawlMatchedLog(MatchedCrawlResult matched)
        {
            var log = new Log
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Name = matched.Result.Name,
                NameForeground = Brush.Chartreuse,
                Content = matched.Result.Title,
                ContentForeground = Brush.DodgerBlue,
                Url = matched.Result.Url
            };

            if (_logs.Count >= 1000)
                _logs.RemoveAt(0);

            Dispatcher.BeginInvokeOnMainThread(() =>
            {
                _logs.Add(log);
                _livLog.ScrollTo(log, ScrollToPosition.End, true);
            });
        }

        private void SetVisibilityManipulationUIContainer(bool visible)
        {
            _gridCrawllistManipulationContainer.IsVisible = visible;

            if (visible)
                _gridCrawllistManipulationContainer.HeightRequest = 40;
            else
                _gridCrawllistManipulationContainer.HeightRequest = 0;
        }


        private void SetVisibilityCommonTaskOptionUIContainer(bool visible)
        {
            _gridCommonTaskOption.IsVisible = visible;

            if (visible)
                _gridCommonTaskOption.HeightRequest = 90;
            else
                _gridCommonTaskOption.HeightRequest = 0;
        }

        private void SetEnableTaskManipulationUIElements(bool enable)
        {
            _btnCrawlRemove.IsEnabled = enable;
            _btnCrawlModify.IsEnabled = enable;

            if (enable)
            {
                _btnCrawlRemove.TextColor = Brush.Black.Color;
                _btnCrawlModify.TextColor = Brush.Black.Color;
            }
            else
            {
                _btnCrawlRemove.TextColor = _disabledForegroundColor.Color;
                _btnCrawlModify.TextColor = _disabledForegroundColor.Color;
            }

        }

        private async void _btnClose_OnClicked(object sender, EventArgs e)
        {
            Dbg.WrilteLine("애플리케이션 종료 시작");
            DependencyService.Get<IAppCloser>().Close();
            Dbg.WrilteLine("서비스가 성공적으로 제거되었습니다.");
        }


        private async void _btnStartCrawling_OnClick(object sender, EventArgs e)
        {
            if (_isDataNetworkActivated)
            {
                var msgResult = await DisplayAlert("메시지", "데이터 통신이 켜져있습니다. 그래도 시작하시겠습니까?", "Yes", "No");
                if (!msgResult) return;
            }

            _crawlTaskManager.Start();
            _btnStartCrawling.IsEnabled = false;
            _btnStartCrawling.TextColor = _disabledForegroundColor.Color;
            _btnStopCrawling.IsEnabled = true;
            _btnStopCrawling.TextColor = Brush.Black.Color;

            _recStatusBar.Fill = Brush.LawnGreen;

            if (!_crawlTaskManager.HasTask)
                _tblStatusBar.Text = "작업을 등록해주세요.";
            else
                _tblStatusBar.Text = "크롤링 시작 준비중";

            AddNoticeLog("크롤링 시작");
        }

        private void _btnStopCrawling_OnClick(object sender, EventArgs e)
        {
            _crawlTaskManager.Stop();
            _btnStartCrawling.IsEnabled = true;
            _btnStartCrawling.TextColor = Brush.Black.Color;
            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.TextColor = _disabledForegroundColor.Color;
            _recStatusBar.Fill = Brush.PaleVioletRed;
            _tblStatusBar.Text = "정지 상태";
            AddNoticeLog("크롤링 중지");
        }


        private void UpdateDCCrawlTaskUI(CrawlTask task)
        {
            var dcCrawl = task.Crawl.To<DCInsideCrawl>();
            if (dcCrawl == null) throw new Exception("말도 안 돼!");
            _cbDCCrawlCategory.SelectedIndex = (int)dcCrawl.BoardType;
            _tbDCCrawlPage.Text = dcCrawl.Page.ToString();
        }

        private void UpdateFMCrawlTaskUI(CrawlTask task)
        {
            var fmCrawl = task.Crawl.To<FMKoreaCrawl>();
            if (fmCrawl == null) throw new Exception("말도 안 돼!");
            _cbFMCrawlCategory.SelectedIndex = (int)fmCrawl.BoardType;
            _tbFMCrawlPage.Text = fmCrawl.Page.ToString();

            if (fmCrawl.SearchOption != FMSearchOption.None)
            {
                _chkbFMCrawlSearchOptionEnable.IsChecked = true;
                _cbFMCrawlSearchOption.SelectedIndex = (int)fmCrawl.SearchOption;
            }

            if (fmCrawl.SearchContent != "")
            {
                _chkbFMCrawlSearchContentEnable.IsChecked = true;
                _tbFMCrawlSearchContent.Text = fmCrawl.SearchContent;
            }
        }

        private void UpdateCrawlTaskCommonUI(CrawlTask task)
        {
            if (task == null) throw new Exception("말도 안 돼!");
            _cbCrawlMatchType.SelectedIndex = (int)task.MatchType;
            _cbCrawlStringMatchRule.SelectedIndex = (int)task.StringMatchRule;
            _tbCrawlMatchString.Text = task.MatchContent;
            _tbCrawlTaskName.Text = task.TaskName;
        }

        private void _chkbFMCrawlSearchOptionEnable_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (_chkbFMCrawlSearchOptionEnable.IsChecked)
            {
                _tblbFMCrawlSearchOption.TextColor = Brush.Black.Color;
                _cbFMCrawlSearchOption.TextColor = Brush.Black.Color;
                _cbFMCrawlSearchOption.IsEnabled = true;
            }
            else
            {
                _tblbFMCrawlSearchOption.TextColor = _disabledForegroundColor.Color;
                _cbFMCrawlSearchOption.TextColor = _disabledForegroundColor.Color;
                _cbFMCrawlSearchOption.IsEnabled = false;
            }
        }

        private void _chkbFMCrawlSearchContentEnable_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (_chkbFMCrawlSearchContentEnable.IsChecked)
            {
                _tblbFMCrawlSearchContent.TextColor = Brush.Black.Color;
                _tbFMCrawlSearchContent.IsEnabled = true;
                _tbFMCrawlSearchContent.Text = string.Empty;
            }
            else
            {
                _tblbFMCrawlSearchContent.TextColor = _disabledForegroundColor.Color;
                _tbFMCrawlSearchContent.IsEnabled = false;
                _tbFMCrawlSearchContent.Text = "비활성화 됨";
            }
        }

        private void _btnCrawlRemove_Click(object sender, EventArgs e)
        {
            if (_selectedCrawlTask == null)
                return;

            _crawlTaskManager.Unregister(_selectedCrawlTask);
            UpdateCrawlTaskList();
        }

        private void _btnCrawlModify_Click(object sender, EventArgs e)
        {
            if (_selectedCrawlTask == null)
            {
                this.DisplayAlert("수정할 요청을 선택해주세요.");
                return;
            }

            if (AddOrModifyTask(_selectedCrawlTask))
            {
                UpdateCrawlTaskList();
                _tbcCrawl_OnSelectionChanged(null, null);
            }
        }

        private void _btnCrawlAdd_Click(object sender, EventArgs e)
        {
            if (AddOrModifyTask(null))
            {
                UpdateCrawlTaskList();
            }
        }

        private bool AddOrModifyTask(CrawlTask modifyTask)
        {

            if (_cbCrawlMatchType.SelectedIndex == -1)
            {
                this.DisplayAlertFireAndForget("매칭 타입을 선택해주세요.");
                return false;
            }

            if (_cbCrawlStringMatchRule.SelectedIndex == -1)
            {
                this.DisplayAlertFireAndForget("문자열 체크 방식을 선택해주세요.");
                return false;
            }

            CrawlMatchType matchType = (CrawlMatchType)_cbCrawlMatchType.SelectedIndex;
            CrawlStringMatchRule stringMatchRule = (CrawlStringMatchRule)_cbCrawlStringMatchRule.SelectedIndex;
            string matchContent = _tbCrawlMatchString.Text.Trim();
            string taskName = _tbCrawlTaskName.Text.Trim();

            if (matchContent.Length == 0)
            {
                this.DisplayAlertFireAndForget("매칭 내용을 입력해주세요.");
                return false;
            }

            if (taskName.Length == 0)
            {
                this.DisplayAlertFireAndForget("작업 이름을 입력해주세요.");
                return false;
            }

            if (_tbcCrawl.TabItems[CrawlType.DCInside].IsSelected)
            {
                DCBoardType boardType = (DCBoardType)_cbDCCrawlCategory.SelectedIndex;
                int.TryParse(_tbDCCrawlPage.Text, out int page);

                if (page <= 0)
                {
                    this.DisplayAlertFireAndForget("페이지를 입력해주세요.");
                    return false;
                }

                if (modifyTask == null)
                    _crawlTaskManager.RegisterDCCrawl(taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, boardType, page);
                else
                    _crawlTaskManager.ModifyDCCrawl(modifyTask, taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, boardType, page);
            }
            else if (_tbcCrawl.TabItems[CrawlType.FMKorea].IsSelected)
            {
                FMBoardType boardType = (FMBoardType)_cbFMCrawlCategory.SelectedIndex;
                int.TryParse(_tbDCCrawlPage.Text, out int page);
                FMSearchOption searchOption = FMSearchOption.None;
                string searchContent = "";

                if (_chkbFMCrawlSearchOptionEnable.IsChecked)
                    searchOption = (FMSearchOption)_cbFMCrawlSearchOption.SelectedIndex;

                if (_chkbFMCrawlSearchContentEnable.IsChecked)
                    searchContent = _tbFMCrawlSearchContent.Text.Trim();

                if (modifyTask == null)
                    _crawlTaskManager.RegisterFMCrawl(taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, searchOption, searchContent, boardType, page);
                else
                    _crawlTaskManager.ModifyFMCrawl(modifyTask, taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, searchOption, searchContent, boardType, page);

            }

            return true;
        }

        private void _tbcCrawl_OnSelectionChanged(object sender, TabSelectionChangedEventArgs e)
        {
            int? pos = e?.NewPosition;

            if (pos == null)
                pos = _tbcCrawl.SelectedIndex;

            if (pos.Value == -1)
                return;

            if (_tbcCrawl.TabItems[pos.Value].IsSelected)
            {
                switch (pos)
                {
                    case CrawlType.FMKorea: SelectFMCrawlTabItem(); break;
                    case CrawlType.DCInside: SelectDCCrawlTabItem(); break;
                    default: SelectSettingTabItem(); return;
                }
            }
        }

        private void SelectDCCrawlTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(true);
            SetVisibilityCommonTaskOptionUIContainer(true);

            _btnCrawlAdd.IsEnabled = true;
            _btnCrawlAdd.TextColor = Brush.Black.Color;
            _selectedCrawlTask = null;

            _tbcCrawl.TabItems[CrawlType.FMKorea].Background = Brush.DarkGray;
            _tbcCrawl.TabItems[CrawlType.DCInside].Background = Brush.Black;
            _tbcCrawl.TabItems[CrawlType.Max].Background = Brush.DarkGray;

            UpdateCrawlTaskList();
        }

        private void SelectSettingTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(false);
            SetVisibilityCommonTaskOptionUIContainer(false);
            _btnCrawlAdd.IsEnabled = false;
            _btnCrawlAdd.TextColor = _disabledForegroundColor.Color;
            _selectedCrawlTask = null;

            _tbcCrawl.TabItems[CrawlType.FMKorea].Background = Brush.DarkGray;
            _tbcCrawl.TabItems[CrawlType.DCInside].Background = Brush.DarkGray;
            _tbcCrawl.TabItems[CrawlType.Max].Background = Brush.Black;
        }

        private void SelectFMCrawlTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(true);
            SetVisibilityCommonTaskOptionUIContainer(true);

            _btnCrawlAdd.IsEnabled = true;
            _btnCrawlAdd.TextColor = Brush.Black.Color;
            _selectedCrawlTask = null;

            _tbcCrawl.TabItems[CrawlType.FMKorea].Background = Brush.Black;
            _tbcCrawl.TabItems[CrawlType.DCInside].Background = Brush.DarkGray;
            _tbcCrawl.TabItems[CrawlType.Max].Background = Brush.DarkGray;

            UpdateCrawlTaskList();

        }

        private void UpdateCrawlTaskList()
        {
            int selectedCralType = -1;

            if (_tbcCrawl.TabItems[CrawlType.FMKorea].IsSelected) selectedCralType = CrawlType.FMKorea;
            else if (_tbcCrawl.TabItems[CrawlType.DCInside].IsSelected) selectedCralType = CrawlType.DCInside;
            else return;

            ListView liv = null;

            switch (selectedCralType)
            {
                case CrawlType.DCInside: liv = _livDCCrawlList; break;
                case CrawlType.FMKorea: liv = _livFMCrawlList; break;
            }

            if (liv == null) return;

            Dispatcher.BeginInvokeOnMainThread(() =>
            {
                _crawlTaskList[selectedCralType].Clear();

                _crawlTaskManager.ForEach(task =>
                {
                    if (task.CrawlType != selectedCralType)
                        return;

                    _crawlTaskList[selectedCralType].Add(task);
                });
            });

        }

        private async void _livLog_OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            var log = _livLog.SelectedItem as Log;
            if (log == null || string.IsNullOrWhiteSpace(log.Url)) return;
            await Browser.OpenAsync(log.Url);
        }

        private void CrawlList_OnItemTapped(object sender, ItemTappedEventArgs e)
        {
            var liv = sender as ListView;
            var task = liv?.SelectedItem as CrawlTask;

            if (task == null)
                return;

            _selectedCrawlTask = task;
            SetEnableTaskManipulationUIElements(true);

            UpdateCrawlTaskCommonUI(_selectedCrawlTask);

            if (_selectedCrawlTask.CrawlType == CrawlType.DCInside)
                UpdateDCCrawlTaskUI(_selectedCrawlTask);
            else if (_selectedCrawlTask.CrawlType == CrawlType.FMKorea)
                UpdateFMCrawlTaskUI(_selectedCrawlTask);
        }

        private void _chkbDCCrawlEnable_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value)
                _crawlTaskManager.BlockedCrawl[CrawlType.DCInside] = false;
            else
                _crawlTaskManager.BlockedCrawl[CrawlType.DCInside] = true;
        }

        private void _chkbFMCrawlEnable_OnCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value)
                _crawlTaskManager.BlockedCrawl[CrawlType.FMKorea] = false;
            else
                _crawlTaskManager.BlockedCrawl[CrawlType.FMKorea] = true;
        }


        private void _btnSaveSetting_OnClicked(object sender, EventArgs e)
        {
            if (_crawlTaskManager.Running)
            {
                this.DisplayAlertFireAndForget("먼저 정지를 해주세요.");
                return;
            }

            if (_tbDCCrawlDelay.Text.Length == 0)
            {
                this.DisplayAlertFireAndForget("디시인사이드 크롤링 주기를 입력해주세요.");
                return;
            }

            if (_tbFMCrawlDelay.Text.Length == 0)
            {
                this.DisplayAlertFireAndForget("에펨코리아 크롤링 주기를 입력해주세요.");
                return;
            }

            int.TryParse(_tbDCCrawlDelay.Text, out int dcCrawlDelay);
            int.TryParse(_tbFMCrawlDelay.Text, out int fmCrawlDelay);

            if (dcCrawlDelay < 1000 || fmCrawlDelay < 1000)
            {
                this.DisplayAlertFireAndForget("크롤링 주기는 1000이상 입력해주세요.");
                return;
            }

            _crawlTaskManager.CrawlDelay[CrawlType.DCInside] = dcCrawlDelay;
            _crawlTaskManager.CrawlDelay[CrawlType.FMKorea] = fmCrawlDelay;

            _crawlTaskManager.ApplyDelay();
            _crawlTaskManager.SaveTaskFile();
            AddNoticeLog("적용완료");
        }


        private async void _btnClearCompletes_OnClicked(object sender, EventArgs e)
        {
            var msgResult = await DisplayAlert("메시지", "완료 목록을 정말로 초기화 하시겠습니가?", "yes", "no");
            if (msgResult)
            {
                _crawlTaskManager.ClearCompletes();
                AddNoticeLog("완료 목록 초기화완료");
            }
        }

        private async void _btnClearTasks_OnClicked(object sender, EventArgs e)
        {
            var msgResult = await DisplayAlert("메시지", "요청 목록을 정말로 초기화 하시겠습니가?", "yes", "no");
            if (msgResult)
            {
                _crawlTaskManager.ClearTasks();
                UpdateCrawlTaskList();
                AddNoticeLog("요청 목록 초기화완료");
            }
        }

        private void _btnReloadSetting_OnClick(object sender, EventArgs e)
        {
            _tbDCCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.DCInside].ToString();
            _tbFMCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.FMKorea].ToString();
            _chkbDCCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.DCInside];
            _chkbFMCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.FMKorea];
            AddNoticeLog("설정 리로드 완료");
        }


        private async void _btnStopForegroundService_OnClicked(object sender, EventArgs e)
        {
            await DependencyService.Get<IForegroundServiceController>().StopForegroundServiceAsync();

            
        }

        private void _btnStartForegroundService_OnClicked(object sender, EventArgs e)
        {
            DependencyService.Get<IForegroundServiceController>().StartForegroundService();
        }
    }
}
