using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Media;
using System.Text;
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

namespace WindowsApp.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CrawlTaskManager _crawlTaskManager = new CrawlTaskManager(5000);

        public MainWindow()
        {
            InitializeComponent();
            _crawlTaskManager.Start();

            _crawlTaskManager.RegisterFMCrawl("ㅇㅇ", CrawlMatchRule.Contain, CrawlMatchType.NickName, FMBoardType.전체, 1);

            // _crawlTaskManager.RegisterDCCrawl("ㅇㅇ", CrawlMatchRule.Contain, CrawlMatchType.NickName, DCBoardType.전체글, 1);
            _crawlTaskManager.OnCrawlSuccess += (crawl, result) => Debug.WriteLine($"{result.Count} 작업완료");
        }

        private void MainWindow_OnClosed(object? sender, EventArgs e)
        {
            _crawlTaskManager.Stop();
        }
    }
}
