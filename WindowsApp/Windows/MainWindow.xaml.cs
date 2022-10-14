using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
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
        public Brush ContentForeground { get; set; }
        public TextDecorationCollection ContentDecoration { get; set; }

        public string Url { get; set; }
        
    }


    public partial class MainWindow : Window
    {
        private CrawlTaskManager _crawlTaskManager = new CrawlTaskManager(5000);
        private bool _windowLoaded = false;
        private Brush _disabledForegroundColor = new SolidColorBrush(Color.FromRgb(126, 126, 126));


        public MainWindow()
        {
            InitializeComponent();
            InitializeDefaultUIStates();
            
            _crawlTaskManager.RegisterFMCrawl("안뇽", "ㅇㅇ", CrawlStringMatchRule.Contain, CrawlMatchType.NickName, 30, FMBoardType.전체, 1);
            _crawlTaskManager.OnCrawlSuccess += (crawl, result) => Debug.WriteLine($"{result.Count} 작업완료");
        }

        private void InitializeDefaultUIStates()
        {
            _chkbFMCrawlSearchOptionEnable_OnUnchecked(null, null);
            _chkbFMCrawlSearchContentEnable_OnUnchecked(null, null);
            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.Foreground = _disabledForegroundColor;
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
            tb.TextDecorations = TextDecorations.Underline;
        }

        private void TextBlock_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not TextBlock tb) return;
            tb.TextDecorations = null;
        }

        private void Number_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "[0-9]+");
        }

        private void _livLog_ItemClick(object sender, RoutedEventArgs e)
        {
            Log? log = (sender as Button)?.DataContext as Log;
            if (log == null) return;
        }


        private void SetEnableListManipulationUIContainer(bool enable)
        {
            _gridCrawllistManipulationContainer.IsEnabled = enable;
        }

        private void SetVisibilityManipulationUIContainer(bool visible)
        {
            _gridCrawllistManipulationContainer.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

            if (visible)
                _gridCrawllistManipulationContainer.Height = 30;
            else
                _gridCrawllistManipulationContainer.Height = 0;
        }

        private void SetEnableCommonTaskOptionUIContainer(bool enable)
        {
            _gridCommonTaskOption.IsEnabled = enable;
        }

        private void SetVisibilityCommonTaskOptionUIContainer(bool visible)
        {
            _gridCommonTaskOption.Visibility = visible ? Visibility.Visible : Visibility.Hidden;

            if (visible)
                _gridCommonTaskOption.Height = 90;
            else
                _gridCommonTaskOption.Height = 0;
        }


        private void TabControl_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_windowLoaded) return;

            switch (_tbcCrawl.SelectedIndex)
            {
                case CrawlType.DCInside:
                    SetVisibilityManipulationUIContainer(true);
                    SetVisibilityCommonTaskOptionUIContainer(true);
                    UpdateCrawlTaskList(CrawlType.DCInside);
                    break;
                case CrawlType.FMKorea:
                    SetVisibilityManipulationUIContainer(true);
                    SetVisibilityCommonTaskOptionUIContainer(true);
                    UpdateCrawlTaskList(CrawlType.FMKorea);
                    break;
                default:
                    SetVisibilityManipulationUIContainer(false);
                    SetVisibilityCommonTaskOptionUIContainer(false);
                    break;
            }

        }

        private void UpdateCrawlTaskList(int crawlType)
        {
            _livDCCrawlList.Items.Clear();
            _crawlTaskManager.ForEach(task =>
            {
                if (task.CrawlType != crawlType)
                    return;

                Dispatcher.Invoke(() =>
                    _livDCCrawlList.Items.Add(new ListViewItem {
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

        private void _livDCCrawlList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void _btnStartCrawling_OnClick(object sender, RoutedEventArgs e)
        {
            _crawlTaskManager.Start();
            _btnStartCrawling.IsEnabled = false;
            _btnStartCrawling.Foreground = _disabledForegroundColor;
            _btnStopCrawling.IsEnabled = true;
            _btnStartCrawling.Foreground = Brushes.Black;

            AddNoticeLog("크롤링 시작");
        }

        private void _btnStopCrawling_OnClick(object sender, RoutedEventArgs e)
        {
            _crawlTaskManager.Start();
            _btnStartCrawling.IsEnabled = true;
            _btnStartCrawling.Foreground = Brushes.Black;
            _btnStopCrawling.IsEnabled = false;
            _btnStopCrawling.Foreground = _disabledForegroundColor;

            AddNoticeLog("크롤링 중지");
        }

        private void AddNoticeLog(string content, string url = "")
        {
            _livLog.Items.Add(new Log()
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Name = "[공지]",
                NameForeground = Brushes.Black,
                Content = content,
                ContentForeground = Brushes.Black
            });
        }

        private void AddCrawlMatchedLog(string name, string content, string url)
        {
            _livLog.Items.Add(new Log()
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Name = name,
                NameForeground = Brushes.Chartreuse,
                Content = content,
                ContentForeground = Brushes.DodgerBlue,
                ContentDecoration = TextDecorations.Underline
            });
        }
    }
}
