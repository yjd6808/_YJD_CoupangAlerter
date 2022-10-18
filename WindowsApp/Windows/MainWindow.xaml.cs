/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RequestApi.Crawl;
using RequestApi.Crawl.Result;
using WindowsApp.Classes.Utils;

namespace WindowsApp.Windows
{
    public class Log
    {
        public string Time { get; set; }
        public string Name { get; set; }
        public Brush NameForeground { get; set; }
        public TextDecorationCollection NameDecoration { get; set; }
        public string Content { get; set; }
        public string CrawlType { get; set; }
        public Brush ContentForeground { get; set; }
        public TextDecorationCollection ContentDecoration { get; set; }
        public string Url { get; set; }
    }


    public partial class MainWindow : Window
    {
        private CrawlTaskManager _crawlTaskManager = new CrawlTaskManager();
        private bool _windowLoaded = false;
        private Brush _disabledForegroundColor = new SolidColorBrush(Color.FromRgb(126, 126, 126));
        private bool[] _selectedTabItem = new bool[CrawlType.Max + 1];
        private CrawlTask? _selectedCrawlTask;
        private SpeechSynthesizer _speechSynthesizer = new SpeechSynthesizer();
        private volatile bool _speeching = false;

        private Thread th1;
        private Thread th2;

        public MainWindow()
        {
            _crawlTaskManager.SetConfigDirectory(System.IO.Path.Combine(Environment.CurrentDirectory, "config"));
            _crawlTaskManager.TryLoadTaskFile();
            _crawlTaskManager.TryLoadCompleteFile();
            _crawlTaskManager.OnCrawlRequest += OnCrawlRequest;
            _crawlTaskManager.OnCrawlSuccess += OnCrawlSuccess;
            _crawlTaskManager.OnCrawlFailed += OnCrawlFailed;
            _crawlTaskManager.OnCrawlMatched += OnCrawlMatched;

            _speechSynthesizer.SetOutputToDefaultAudioDevice();

            InitializeComponent();
            InitializeDefaultUIStates();

            //Thread t1 = new Thread(() =>
            //{
            //    while (true)
            //    {
            //        lock (this)
            //        {
                        
            //            Monitor.Wait(this);
            //            Debug.WriteLine("t1 wake up!!");
            //        }
            //    }
            //});

            //Thread t2 = new Thread(() =>
            //{
            //    while (true)
            //    {
            //        Thread.Sleep(1000);
            //        lock (this)
            //        {
            //            Monitor.Pulse(this);
            //            Debug.WriteLine("t2 pulse to t1");
            //        }
            //    }
            //});

            //Thread t3 = new Thread(() =>
            //{
            //    Thread.Sleep(500);
            //    lock (this)
            //    {
            //        Monitor.Pulse(this);
            //        Debug.WriteLine("t3 pulse to t1");
            //    }
            //});
            //t1.Start();
            //t2.Start();
            //t3.Start();

            //while (true)
            //{
                
            //}

        }

        private void OnCrawlRequest(CrawlTask task)
        {
            UpdateStatusBar();
        }

        private void OnCrawlMatched(CrawlTask crawl, MatchedCrawlResult matchedresult)
        {
            UpdateStatusBar();
            AddCrawlMatchedLog(matchedresult);

            if (_speeching)
                return;

            Task.Run(() =>
            {
                _speeching = true;
                _speechSynthesizer.Speak("found matched result!");
            }).ContinueWith(x => _speeching = false);
        }

        private void OnCrawlFailed(CrawlTask task, string errorMessage)
        {
            UpdateStatusBar();

            if (errorMessage != string.Empty)
                Dispatcher.BeginInvoke(() => AddNoticeLog(errorMessage));
        }

        private void OnCrawlSuccess(CrawlTask crawl, List<CrawlResult> crawlresult)
        {
            UpdateStatusBar();

            //Dispatcher.BeginInvoke(() => AddCrawlTaskLog(crawl, crawlresult));
        }

        private void UpdateStatusBar()
        {
            Dispatcher.BeginInvoke(() => _tblStatusBar.Text = 
                $"요청: {_crawlTaskManager.Stat.RequestCount} " +
                $"성공: {_crawlTaskManager.Stat.RequestSuccessCount} " +
                $"실패: {_crawlTaskManager.Stat.RequestFailedCount} " +
                $"매칭: {_crawlTaskManager.Stat.RequestMatchedCount}");
        }

        private void InitializeDefaultUIStates()
        {
            _chkbFMCrawlSearchOptionEnable_OnUnchecked(null, null);
            _chkbFMCrawlSearchContentEnable_OnUnchecked(null, null);
            SelectDCCrawlTabItem();

            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.Foreground = _disabledForegroundColor;

            _chkbDCCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.DCInside];
            _chkbFMCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.FMKorea];
            _tbDCCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.DCInside].ToString();
            _tbFMCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.FMKorea].ToString();
        }

        private void MainWindow_OnClosed(object? sender, EventArgs e)
        {
            if (_crawlTaskManager.Running)
                _crawlTaskManager.Stop();
        }

        private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TextBlock_OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not TextBlock tb) return;
            if (tb.IsEnabled == false) return;
            tb.TextDecorations = TextDecorations.Underline;
        }

        private void TextBlock_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not TextBlock tb) return;
            if (tb.IsEnabled == false) return;
            tb.TextDecorations = null;
        }

        private void Number_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "[0-9]+");
        }

        private void _livLog_ItemClick(object sender, RoutedEventArgs e)
        {
            Log? log = (sender as Button)?.DataContext as Log;
            if (log == null || string.IsNullOrWhiteSpace(log.Url)) return;
            Process.Start(log.Url);
        }



        private void SetVisibilityManipulationUIContainer(bool visible)
        {
            _gridCrawllistManipulationContainer.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

            if (visible)
                _gridCrawllistManipulationContainer.Height = 30;
            else
                _gridCrawllistManipulationContainer.Height = 0;
        }


        private void SetVisibilityCommonTaskOptionUIContainer(bool visible)
        {
            _gridCommonTaskOption.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

            if (visible)
                _gridCommonTaskOption.Height = 120;
            else
                _gridCommonTaskOption.Height = 0;
        }

        private void SetEnableTaskManipulationUIElements(bool enable)
        {
            _btnCrawlRemove.IsEnabled = enable;
            _btnCrawlModify.IsEnabled = enable;

            if (enable)
            {
                _btnCrawlRemove.Foreground = Brushes.Black;
                _btnCrawlModify.Foreground = Brushes.Black;
            }
            else
            {
                _btnCrawlRemove.Foreground = _disabledForegroundColor;
                _btnCrawlModify.Foreground = _disabledForegroundColor;
            }

        }



        private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_windowLoaded) return;

            // 탭컨트롤 내부 엘리먼트 클릭을 해도 여기 들어옴 ㄷㄷ..
            // 그래서 이전 탭컨트롤 상태값을 관리하는 _tabItemSelected를 추가해줌.

            if (!_selectedTabItem[CrawlType.DCInside] && _tbiDCCrawl.IsSelected)
                SelectDCCrawlTabItem();
            else if (!_selectedTabItem[CrawlType.FMKorea] && _tbiFMCrawl.IsSelected)
                SelectFMCrawlTabItem();
            else if (!_selectedTabItem[CrawlType.Max] && _tbiSetting.IsSelected)
                SelectSettingTabItem();
        }

     

        

        private void SelectDCCrawlTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(true);
            SetVisibilityCommonTaskOptionUIContainer(true);

            _selectedTabItem[CrawlType.DCInside] = true;
            _selectedTabItem[CrawlType.FMKorea] = false;
            _selectedTabItem[CrawlType.Max] = false;
            _btnCrawlAdd.IsEnabled = true;
            _btnCrawlAdd.Foreground = Brushes.Black;
            _selectedCrawlTask = null;

            UpdateCrawlTaskList();
        }

        private void SelectSettingTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(false);
            SetVisibilityCommonTaskOptionUIContainer(false);
            _selectedTabItem[CrawlType.DCInside] = false;
            _selectedTabItem[CrawlType.FMKorea] = false;
            _selectedTabItem[CrawlType.Max] = true;
            _btnCrawlAdd.IsEnabled = false;
            _btnCrawlAdd.Foreground = _disabledForegroundColor;
            _selectedCrawlTask = null;
        }

        private void SelectFMCrawlTabItem()
        {
            SetEnableTaskManipulationUIElements(false);
            SetVisibilityManipulationUIContainer(true);
            SetVisibilityCommonTaskOptionUIContainer(true);

            _selectedTabItem[CrawlType.DCInside] = false;
            _selectedTabItem[CrawlType.FMKorea] = true;
            _selectedTabItem[CrawlType.Max] = false;
            _btnCrawlAdd.IsEnabled = true;
            _btnCrawlAdd.Foreground = Brushes.Black;
            _selectedCrawlTask = null;

            UpdateCrawlTaskList();

        }
        private void UpdateCrawlTaskList()
        {
            int selectedCralType = -1;

            if (_selectedTabItem[CrawlType.FMKorea]) selectedCralType = CrawlType.FMKorea;
            else if (_selectedTabItem[CrawlType.DCInside]) selectedCralType = CrawlType.DCInside;
            else return;

            ListView? liv = null;

            switch (selectedCralType)
            {
                case CrawlType.DCInside: liv = _livDCCrawlList; break;
                case CrawlType.FMKorea: liv = _livFMCrawlList; break;
            }

            liv.Items.Clear();
            _crawlTaskManager.ForEach(task =>
            {
                if (task.CrawlType != selectedCralType)
                    return;

                Dispatcher.Invoke(() =>
                    liv.Items.Add(new ListViewItem {
                        DataContext = task,
                        Content = task.TaskName
                    })
                );
            });
        }

      
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _windowLoaded = true;
        }

        private void _chkbFMCrawlSearchOptionEnable_OnChecked(object sender, RoutedEventArgs e)
        {
            _tblbFMCrawlSearchOption.Foreground = Brushes.Black;
            _cbFMCrawlSearchOption.Foreground = Brushes.Black;
            _cbFMCrawlSearchOption.IsEnabled = true;
        }

        private void _chkbFMCrawlSearchOptionEnable_OnUnchecked(object sender, RoutedEventArgs e)
        {
            _tblbFMCrawlSearchOption.Foreground = _disabledForegroundColor;
            _cbFMCrawlSearchOption.Foreground = _disabledForegroundColor;
            _cbFMCrawlSearchOption.IsEnabled = false;
        }

        private void _chkbFMCrawlSearchContentEnable_OnChecked(object sender, RoutedEventArgs e)
        {
            _tblbFMCrawlSearchContent.Foreground = Brushes.Black;
            _tbFMCrawlSearchContent.IsEnabled = true;
            _tbFMCrawlSearchContent.Text = string.Empty;
        }

        private void _chkbFMCrawlSearchContentEnable_OnUnchecked(object sender, RoutedEventArgs e)
        {
            _tblbFMCrawlSearchContent.Foreground = _disabledForegroundColor;
            _tbFMCrawlSearchContent.IsEnabled = false;
            _tbFMCrawlSearchContent.Text = "비활성화 됨";
        }

     

        private void _btnStartCrawling_OnClick(object sender, RoutedEventArgs e)
        {
            _crawlTaskManager.Start();
            _btnStartCrawling.IsEnabled = false;
            _btnStartCrawling.Foreground = _disabledForegroundColor;
            _btnStopCrawling.IsEnabled = true;
            _btnStopCrawling.Foreground = Brushes.Black;

            _recStatusBar.Fill = Brushes.LawnGreen;

            if (!_crawlTaskManager.HasTask)
                _tblStatusBar.Text = "작업을 등록해주세요.";
            else
                _tblStatusBar.Text = "크롤링 시작 준비중";

            AddNoticeLog("크롤링 시작");
        }

        private void _btnStopCrawling_OnClick(object sender, RoutedEventArgs e)
        {
            _crawlTaskManager.Stop();
            _btnStartCrawling.IsEnabled = true;
            _btnStartCrawling.Foreground = Brushes.Black;
            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.Foreground = _disabledForegroundColor;
            _recStatusBar.Fill = Brushes.PaleVioletRed;
            _tblStatusBar.Text = "정지 상태";
            AddNoticeLog("크롤링 중지");
        }

        private void AddNoticeLog(string content, string url = "")
        {
            var log = new Log
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Name = "[공지]",
                NameForeground = Brushes.Black,
                Content = content,
                ContentForeground = Brushes.Black
            };

            _livLog.Items.Add(log);
            _livLog.ScrollIntoView(log);
        }

        private void AddCrawlMatchedLog(MatchedCrawlResult matched)
        {
            var log = new Log
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                CrawlType = CrawlType.ToStringInitial(matched.Result.CrawlType),
                Name = matched.Result.Name,
                NameForeground = Brushes.ForestGreen,
                Content = matched.Result.Title,
                ContentForeground = Brushes.DodgerBlue,
                ContentDecoration = TextDecorations.Underline,
                Url = matched.Result.Url
            };

            Dispatcher.BeginInvoke(() =>
            {
                _livLog.Items.Add(log);
                _livLog.ScrollIntoView(log);
            });
        }

        private void AddCrawlTaskLog(CrawlTask task, List<CrawlResult> results)
        {
            var log = new Log
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                CrawlType = CrawlType.ToStringInitial(task.CrawlType),
                Content = results.Count + " 데이터 있음",
                ContentForeground = Brushes.Black
            };

            Dispatcher.BeginInvoke(() =>
            {
                _livLog.Items.Add(log);
                _livLog.ScrollIntoView(log);
            });
        }


        private void CrawlList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var liv = sender as ListView;
            var item = liv?.SelectedItem as ListViewItem;
            var task = item?.DataContext as CrawlTask;

            if (task == null)
                return;

            _selectedCrawlTask = task;
            SetEnableTaskManipulationUIElements(true);
        }

        private void CrawlList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedCrawlTask == null)
                return;

            UpdateCrawlTaskCommonUI(_selectedCrawlTask);

            if (_selectedCrawlTask.CrawlType == CrawlType.DCInside)
                UpdateDCCrawlTaskUI(_selectedCrawlTask);
            else if (_selectedCrawlTask.CrawlType == CrawlType.FMKorea)
                UpdateFMCrawlTaskUI(_selectedCrawlTask);
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

        private void _btnCrawlAdd_Click(object sender, RoutedEventArgs e)
        {
            AddOrModifyTask(null);
            UpdateCrawlTaskList();
        }
        private void _btnCrawlRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCrawlTask == null)
                return;

            _crawlTaskManager.Unregister(_selectedCrawlTask);
            UpdateCrawlTaskList();
        }

        private void _btnCrawlModify_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCrawlTask == null)
                return;

            AddOrModifyTask(_selectedCrawlTask);
            UpdateCrawlTaskList();
        }

        private void AddOrModifyTask(CrawlTask? modifyTask)
        {
            if (_cbCrawlMatchType.SelectedIndex == -1)
            {
                MsgBox.ShowTopMost("매칭 타입을 선택해주세요.");
                return;
            }

            if (_cbCrawlStringMatchRule.SelectedIndex == -1)
            {
                MsgBox.ShowTopMost("문자열 체크 방식을 선택해주세요.");
                return;
            }

            CrawlMatchType matchType = (CrawlMatchType)_cbCrawlMatchType.SelectedIndex;
            CrawlStringMatchRule stringMatchRule = (CrawlStringMatchRule)_cbCrawlStringMatchRule.SelectedIndex;
            string matchContent = _tbCrawlMatchString.Text.Trim();
            string taskName = _tbCrawlTaskName.Text.Trim();

            if (matchContent.Length == 0)
            {
                MsgBox.ShowTopMost("매칭 내용을 입력해주세요.");
                return;
            }

            if (taskName.Length == 0)
            {
                MsgBox.ShowTopMost("작업 이름을 입력해주세요.");
                return;
            }

            if (_selectedTabItem[CrawlType.DCInside])
            {
                DCBoardType boardType = (DCBoardType)_cbDCCrawlCategory.SelectedIndex;
                int.TryParse(_tbDCCrawlPage.Text, out int page);

                if (page <= 0)
                {
                    MsgBox.ShowTopMost("페이지를 입력해주세요.");
                    return;
                }

                if (modifyTask == null)
                    _crawlTaskManager.RegisterDCCrawl(taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, boardType, page);
                else
                    _crawlTaskManager.ModifyDCCrawl(modifyTask, taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, boardType, page);
            }
            else if (_selectedTabItem[CrawlType.FMKorea])
            {
                FMBoardType boardType = (FMBoardType)_cbFMCrawlCategory.SelectedIndex;
                int.TryParse(_tbDCCrawlPage.Text, out int page);
                FMSearchOption searchOption = FMSearchOption.None;
                string searchContent = "";

                if (_chkbFMCrawlSearchOptionEnable.IsChecked != null &&
                    _chkbFMCrawlSearchOptionEnable.IsChecked.Value)
                    searchOption = (FMSearchOption)_cbFMCrawlSearchOption.SelectedIndex;

                if (_chkbFMCrawlSearchContentEnable.IsChecked != null &&
                    _chkbFMCrawlSearchContentEnable.IsChecked.Value)
                    searchContent = _tbFMCrawlSearchContent.Text.Trim();

                if (modifyTask == null)
                    _crawlTaskManager.RegisterFMCrawl(taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, searchOption, searchContent, boardType, page);
                else
                    _crawlTaskManager.ModifyFMCrawl(modifyTask, taskName, matchContent, stringMatchRule, matchType, CrawlTaskManager.DefaultMatchingRange, searchOption, searchContent, boardType, page);
            }
        }

       

        private void _chkbDCCrawlEnable_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_chkbDCCrawlEnable.IsChecked == null)
                return;

            if (_chkbDCCrawlEnable.IsChecked.Value)
                _crawlTaskManager.BlockedCrawl[CrawlType.DCInside] = false;
            else
                _crawlTaskManager.BlockedCrawl[CrawlType.DCInside] = true;
        }

        private void _chkbFMCrawlEnable_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_chkbFMCrawlEnable.IsChecked == null)
                return;

            if (_chkbFMCrawlEnable.IsChecked.Value)
                _crawlTaskManager.BlockedCrawl[CrawlType.FMKorea] = false;
            else
                _crawlTaskManager.BlockedCrawl[CrawlType.FMKorea] = true;
        }

        private void _btnSaveSetting_OnClicked(object sender, EventArgs e)
        {
            if (_crawlTaskManager.Running)
            {
                MsgBox.ShowTopMost("먼저 정지를 해주세요.");
                return;
            }

            if (_tbDCCrawlDelay.Text.Length == 0)
            {
                MsgBox.ShowTopMost("디시인사이드 크롤링 주기를 입력해주세요.");
                return;
            }

            if (_tbFMCrawlDelay.Text.Length == 0)
            {
                MsgBox.ShowTopMost("에펨코리아 크롤링 주기를 입력해주세요.");
                return;
            }

            int.TryParse(_tbDCCrawlDelay.Text, out int dcCrawlDelay);
            int.TryParse(_tbFMCrawlDelay.Text, out int fmCrawlDelay);

            if (dcCrawlDelay < 1000 || fmCrawlDelay < 1000)
            {
                MsgBox.ShowTopMost("크롤링 주기는 1000이상 입력해주세요.");
                return;
            }

            _crawlTaskManager.CrawlDelay[CrawlType.DCInside] = dcCrawlDelay;
            _crawlTaskManager.CrawlDelay[CrawlType.FMKorea] = fmCrawlDelay;

            _crawlTaskManager.ApplyDelay();
            _crawlTaskManager.SaveTaskFile();
            AddNoticeLog("적용완료");
        }
        

        private void _btnClearCompletes_OnClicked(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("완료 목록을 정말로 초기화 하시겠습니가?", "메시지", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _crawlTaskManager.ClearCompletes();
                AddNoticeLog("완료 목록 초기화완료");
            }
        }

        private void _btnClearTasks_OnClicked(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("요청 목록을 정말로 초기화 하시겠습니가?", "메시지", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _crawlTaskManager.ClearTasks();
                UpdateCrawlTaskList();
                AddNoticeLog("요청 목록 초기화완료");
            }
        }

        private void _btnReloadSetting_OnClick(object sender, RoutedEventArgs e)
        {
            _tbDCCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.DCInside].ToString();
            _tbFMCrawlDelay.Text = _crawlTaskManager.CrawlDelay[CrawlType.FMKorea].ToString();
            _chkbDCCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.DCInside];
            _chkbFMCrawlEnable.IsChecked = !_crawlTaskManager.BlockedCrawl[CrawlType.FMKorea];
            AddNoticeLog("설정 리로드 완료");
        }
    }
}
