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

        protected override void OnNewIntent(Intent intent)
        {
            CreateNotificationFromIntent(intent);

            NotificationManager notificationManager = (NotificationManager)GetSystemService(Context.NotificationService);
            notificationManager.CancelAll();
        }

        void CreateNotificationFromIntent(Intent intent)
        {
            // if (intent.Action == "notification") 설정한대로 안들어오고 "android.Intent.MAIN" 뭐 이런거 들어오네

            // 아래 기능이 실행이안되네
            if (intent.Extras != null)
            {
                string title = intent.GetStringExtra(AndroidNotificationManager.TitleKey);
                string message = intent.GetStringExtra(AndroidNotificationManager.MessageKey);
                int messageId = intent.GetIntExtra(AndroidNotificationManager.MesssageIdKey, -1);
                DependencyService.Get<INotificationManager>().ReceiveNotification(title, message);

                if (messageId != -1)
                {
                    // https://stackoverflow.com/questions/59052033/cancel-notification-when-app-is-removed-from-recentsmemory-list
                    
                }
            }
        }
    }
}