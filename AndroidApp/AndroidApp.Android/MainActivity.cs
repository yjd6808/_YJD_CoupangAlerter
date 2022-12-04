using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidApp.Classes.Services.App;
using AndroidApp.Classes.Services.Notification;
using AndroidApp.Classes.Services.State;
using AndroidApp.Classes.Utils;
using AndroidApp.Droid.Classes.Services.Notification;
using RequestApi.Crawl;
using Xamarin.Essentials;
using Xamarin.Forms;
using static Android.OS.PowerManager;

namespace AndroidApp.Droid
{


    [Activity(Label = "쿠팡 알리미", LaunchMode = LaunchMode.SingleTop, Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public static Classes.Services.State.ForegroundService s_ForegroundService = null;
        public static WakeLock LockApp = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Platform.Init(this, savedInstanceState);
            Forms.Init(this, savedInstanceState);
            LoadApplication(new App());

            // https://learn.microsoft.com/en-us/dotnet/api/android.os.wakelockflags?view=xamarin-android-sdk-12
            // Sleep 모드에서도 항상 쓰레드가 스케쥴링 될 수 있도록 깨워놓자.
            var pm = (PowerManager)GetSystemService(Context.PowerService);
            LockApp = pm.NewWakeLock(WakeLockFlags.Partial, "efsfseef");
            LockApp.Acquire();

            // https://stackoverflow.com/questions/2039555/how-to-get-an-android-wakelock-to-work
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            DependencyService.Get<IForegroundServiceController>().StartPreventingSleep();
            DependencyService.Get<IForegroundServiceController>().StartForegroundService();
        }

        protected override void OnStop()
        {
            base.OnStop();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            DependencyService.Get<IAppCloser>().Close();
            Dbg.WrilteLine("애플리케이션이 종료되었습니다.");
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }


        protected override async void OnNewIntent(Intent intent)
        {
            // ================================== 크롤링 매치 결과 알람을 누른 경우
            //string title = intent.GetStringExtra(AndroidNotificationManager.TitleKey);
            //string message = intent.GetStringExtra(AndroidNotificationManager.MessageKey);
            int messageId = intent.GetIntExtra(AndroidNotificationManager.MesssageIdKey, -1);

            NotificationManager notificationManager = (NotificationManager)GetSystemService(Context.NotificationService);
            // 포그라운드 알림 메시지가 크롤링 매칭결과 알림 메시지중 1개로 변조되어 보이는 현상때문에
            // 서비스 정지시켰다가 다시 시작시키는 방식으로 한다.
            await DependencyService.Get<IForegroundServiceController>().StopForegroundServiceAsync();

            //notificationManager.Cancel(AndroidNotificationManager.MesssageIdKey, messageId);
            notificationManager.CancelAll();
            if (messageId != -1)
                notificationManager.Cancel(messageId);

            DependencyService.Get<IForegroundServiceController>().StartForegroundService();
        }
    }
}