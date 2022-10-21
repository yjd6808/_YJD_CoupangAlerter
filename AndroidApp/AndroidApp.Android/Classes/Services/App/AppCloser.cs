using Android.App;
using Android.Content;
using Android.OS;
using AndroidApp.Classes.Services.App;
using AndroidApp.Classes.Services.State;
using RequestApi.Crawl;
using Xamarin.Forms;


[assembly: Dependency(typeof(AndroidApp.Droid.Classes.Services.App.AppCloser))]
namespace AndroidApp.Droid.Classes.Services.App
{
    internal class AppCloser : IAppCloser
    {
        public async void Close()
        {
            MainActivity.LockApp.Release();
            CrawlTaskManager crawlTaskManager = DependencyService.Get<IAppInitializer>().GetValue<CrawlTaskManager>(typeof(CrawlTaskManager));

            if (crawlTaskManager.Running)
                crawlTaskManager.Stop();

            // 상단에 알림들 모두 제거해주자.
            NotificationManager notificationManager = (NotificationManager)Android.App.Application.Context.GetSystemService(Context.NotificationService);
            notificationManager.CancelAll();

            DependencyService.Get<IForegroundServiceController>().StopPreventingSleep();
            await DependencyService.Get<IForegroundServiceController>().StopForegroundServiceAsync();

            // https://stackoverflow.com/questions/29257929/how-to-terminate-a-xamarin-application
            Process.KillProcess(Process.MyPid());
        }
    }
}