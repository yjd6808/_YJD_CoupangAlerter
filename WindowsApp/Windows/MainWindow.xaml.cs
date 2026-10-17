/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * * * * * * * * * * * * *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using RequestApi.Crawl;
using RequestApi.Crawl.Result;
using RequestApi.Utils;
using WindowsApp.Classes.Utils;

namespace WindowsApp.Windows
{
    // 매칭 결과 표시용 DTO
    public class MatchedResultView
    {
        public long PostId { get; set; }
        public string Title { get; set; }
        public string Name { get; set; }
        public string WriteDate { get; set; }
        public string Url { get; set; }
    }

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
        [DllImport("user32")] public static extern int FlashWindow(IntPtr hwnd, bool bInvert);
        [DllImport("user32")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        private CrawlTaskManager _crawlTaskManager = new CrawlTaskManager();
        private bool _windowLoaded = false;
        private Brush _disabledForegroundColor = new SolidColorBrush(Color.FromRgb(126, 126, 126));
        private bool[] _selectedTabItem = new bool[CrawlType.Max + 1];
        private CrawlTask? _selectedCrawlTask;
        private SpeechSynthesizer _speechSynthesizer = new SpeechSynthesizer();
        private volatile bool _speeching = false;
        private Logger _dbgLogger = Logger.GetInstance();
        private System.Windows.Forms.NotifyIcon _notifyIcon;

        public MainWindow()
        {
            _crawlTaskManager.SetConfigDirectory(System.IO.Path.Combine(Environment.CurrentDirectory, "config"));
            _crawlTaskManager.TryLoadTaskFile();
            _crawlTaskManager.OnCrawlRequest += OnCrawlRequest;
            _crawlTaskManager.OnCrawlSuccess += OnCrawlSuccess;
            _crawlTaskManager.OnCrawlFailed += OnCrawlFailed;
            _crawlTaskManager.OnCrawlMatched += OnCrawlMatched;

            _speechSynthesizer.SetOutputToDefaultAudioDevice();
            InitializeComponent();
            InitializeDefaultUIStates();

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.GlassFrameThickness = new Thickness(0);
            WindowChrome.SetWindowChrome(this, chrome);

            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "");
            }
            catch
            {
                // ignored
            }

            _notifyIcon.Text = "CoupangAlerter";
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                ShowInTaskbar = true;
                Activate();
            };

#if DEBUG
            _dbgLogger.OnDebugLog += (code, o) =>
            {
                Dispatcher.Invoke(() =>
                {
                    string log = string.Empty;

                    switch (code)
                    {
                        case LoggerCode.CrawlTaskWorkerTick:
                        {
                            var task = o[0] as CrawlTask;
                            var tick = (int)o[1];
                            log = $"{task.TaskName} {tick} 경과";
                            break;
                        }
                        case LoggerCode.CrawlTaskWorkerTimeOver:
                        {
                            var task = o[0] as CrawlTask;
                            log = $"{task.TaskName} 시간 다됨";
                            break;
                        }
                    }

                    if (_livDebugLog.Items.Count > 5000)
                        _livDebugLog.Items.RemoveAt(0);

                    _dbgLogger.WriteLine(log);
                    _livDebugLog.Items.Add(log);
                    _livDebugLog.ScrollIntoView(_livDebugLog.Items[^1]);
                });
            };
#endif
        }

        private void OnCrawlRequest(CrawlTask task)
        {
            UpdateStatusBar();
        }

        private void OnCrawlMatched(CrawlTask crawl, MatchedCrawlResult matchedresult)
        {
            UpdateStatusBar();
            AddCrawlMatchedLog(matchedresult);
            Dispatcher.BeginInvoke(() => RebuildMatchedResultsView());

            if (matchedresult.IsSuppressed)
                return;

            TryPlayCompleteSound();

            Dispatcher.BeginInvoke(() =>
                MsgBox.ShowTopMost($"[{CrawlType.ToStringInitial(matchedresult.Result.CrawlType)}] {matchedresult.Result.Title}", "매칭 결과")
            );

            if (_speeching)
                return;

            Task.Run(() =>
            {
                _speeching = true;
                _speechSynthesizer.Speak("found matched result!");
            }).ContinueWith(x => _speeching = false);
        }

        private volatile bool _soundPlaying = false;

        private void TryPlayCompleteSound()
        {
            if (_soundPlaying)
                return;

            string wavPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "complete.wav");
            if (!System.IO.File.Exists(wavPath))
                return;

            try
            {
                _soundPlaying = true;
                var player = new System.Media.SoundPlayer(wavPath);
                player.Play();
                Task.Run(() =>
                {
                    try
                    {
                        using (var reader = new System.IO.BinaryReader(System.IO.File.OpenRead(wavPath)))
                        {
                            reader.ReadBytes(4); // RIFF
                            reader.ReadInt32();
                            reader.ReadBytes(8); // WAVE + fmt
                            reader.ReadInt32();
                            reader.ReadInt16();
                            reader.ReadInt16();
                            int sampleRate = reader.ReadInt32();
                            reader.ReadInt32();
                            reader.ReadInt16();
                            int bitsPerSample = reader.ReadInt16();
                            reader.ReadBytes(4); // data
                            int dataSize = reader.ReadInt32();
                            int durationMs = (int)((long)dataSize * 1000 / (sampleRate * bitsPerSample / 8));
                            Thread.Sleep(durationMs + 100);
                        }
                    }
                    catch { Thread.Sleep(3000); }
                    finally { _soundPlaying = false; }
                });
            }
            catch { _soundPlaying = false; }
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

            Dispatcher.BeginInvoke(() =>
                AddNoticeLog($"{crawlresult.Count}개 크롤링 완료 (작업: {crawl.TaskName})")
            );
        }

        private void UpdateStatusBar()
        {
            Dispatcher.BeginInvoke(() => _tblStatusBar.Text = 
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
            _tbMaxMatchedDisplayCount.Text = _crawlTaskManager.MaxMatchedDisplayCount.ToString();
        }

        private void MainWindow_OnClosed(object? sender, EventArgs e)
        {
            if (_crawlTaskManager.Running)
                _crawlTaskManager.Stop();

#if DEBUG
            _dbgLogger.Close();
#endif
        }

        private void MainWindow_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); } catch { }
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (_chkbTrayIcon.IsChecked == true)
            {
                Hide();
                ShowInTaskbar = false;
                _notifyIcon.Visible = true;
            }
            else
            {
                Close();
            }
        }

        private void MainWindow_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_chkbTrayIcon.IsChecked == true && _notifyIcon.Visible)
            {
                Hide();
                ShowInTaskbar = false;
                e.Cancel = true;
                return;
            }

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
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
            Process.Start(new ProcessStartInfo(log.Url) { UseShellExecute = true });
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
            _livDebugLog.Height = 0;

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
            _livDebugLog.Height = 200;
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
            _livDebugLog.Height = 0;

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

        private void _tgbMatchedType_OnChecked(object sender, RoutedEventArgs e)
        {
            if (sender == _tgbMatchedFM && _tgbMatchedDC != null)
                _tgbMatchedDC.IsChecked = false;
            else if (sender == _tgbMatchedDC && _tgbMatchedFM != null)
                _tgbMatchedFM.IsChecked = false;

            RebuildMatchedResultsView();
        }

        private void RebuildMatchedResultsView()
        {
            if (_dgMatchedResults == null)
                return;

            int crawlType = _tgbMatchedFM.IsChecked == true ? CrawlType.FMKorea : CrawlType.DCInside;
            int maxCount = _crawlTaskManager.MaxMatchedDisplayCount;
            var results = _crawlTaskManager.GetMatchedResults(crawlType, maxCount);
            var list = results.Select(r => new MatchedResultView
            {
                PostId = r.Result.PostId,
                Title = r.Result.Title,
                Name = r.Result.Name,
                WriteDate = r.Result.WriteDate.ToString("yyyy-MM-dd HH:mm"),
                Url = r.Result.Url
            }).ToList();

            _dgMatchedResults.ItemsSource = list;
        }

        private void TitleTextBlock_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is MatchedResultView item && !string.IsNullOrWhiteSpace(item.Url))
                Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
        }

        private void _dgMatchedResults_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_dgMatchedResults.SelectedItem is MatchedResultView item && !string.IsNullOrWhiteSpace(item.Url))
                Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
        }

      
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _windowLoaded = true;
            RebuildMatchedResultsView();
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

     

        // 9222 포트로 디버깅이 허용된 엣지 브라우저가 실행 중인지 확인한다.
        // WebFetch(Playwright)가 이 엣지에 CDP로 접속해 크롤링하므로 필수 조건이다.
        private static bool IsCdpEdgeRunning()
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    return client.ConnectAsync("127.0.0.1", 9222).Wait(1000) && client.Connected;
                }
            }
            catch
            {
                return false;
            }
        }

        private void _btnStartCrawling_OnClick(object sender, RoutedEventArgs e)
        {
            if (!IsCdpEdgeRunning())
            {
                MsgBox.ShowTopMost("실행 중인 엣지 브라우저가 없습니다.\n\n" +
                    "엣지를 다음 옵션으로 실행한 뒤 다시 시작해주세요.\n\n" +
                    "msedge.exe -user-data-dir=\"%LOCALAPPDATA%\\Edge-CDP\" --profile-directory=Default --remote-debugging-port=9222");
                return;
            }

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

            if (_livLog.Items.Count >= 300)
            {
	            _livLog.Items.RemoveAt(0);
            }

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
	            if (_livLog.Items.Count >= 300)
	            {
		            _livLog.Items.RemoveAt(0);
	            }

	            _livLog.Items.Add(log);
                _livLog.ScrollIntoView(log);

                WindowInteropHelper wih = new WindowInteropHelper(this);
                FLASHWINFO fi = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                    hwnd = wih.Handle,
                    dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                    uCount = uint.MaxValue,
                    dwTimeout = 0
                };
                FlashWindowEx(ref fi);
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
	            if (_livLog.Items.Count >= 300)
	            {
		            _livLog.Items.RemoveAt(0);
				}

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
                int.TryParse(_tbFMCrawlPage.Text, out int page);

                if (page <= 0)
                {
                    MsgBox.ShowTopMost("페이지를 입력해주세요.");
                    return;
                }

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

            if (int.TryParse(_tbMaxMatchedDisplayCount.Text, out int maxDisplayCount) && maxDisplayCount > 0)
                _crawlTaskManager.MaxMatchedDisplayCount = maxDisplayCount;

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
            _tbMaxMatchedDisplayCount.Text = _crawlTaskManager.MaxMatchedDisplayCount.ToString();
            AddNoticeLog("설정 리로드 완료");
        }

        private void ButtonMinimize_OnClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ButtonPin_OnClick(object sender, RoutedEventArgs e)
        {
            if (!Topmost)
            {
                this.Topmost = true;
                _imgPin.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/PinOff.ico"));
            }
            else
            {
                this.Topmost = false;
                _imgPin.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/Pin.ico"));
            }
        }
    }
}
