using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

namespace WindowsApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DCInsideCrawl crawl = new DCInsideCrawl();
            List<CrawlResult> results = crawl.TryCrawl();

            ;

            FMKoreaCrawl crawl2 = new FMKoreaCrawl(FMSearchOption.TitleContent, "옹", FMBoardType.종목추천_분석, 1);
            List<CrawlResult> results2 = crawl2.TryCrawl();

        }
    }
}
