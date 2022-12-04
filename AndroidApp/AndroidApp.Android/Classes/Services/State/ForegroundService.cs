// 소스코드 원본: https://hijjang2.tistory.com/462

using Android.App;
using Android.Content;
using Android.OS;
using AndroidApp.Classes.Utils;
using AndroidX.Core.App;
using Java.Util;


namespace AndroidApp.Droid.Classes.Services.State
{

    // https://learn.microsoft.com/ko-kr/xamarin/android/app-fundamentals/services/creating-a-service/started-services

    // Service 어트리뷰트를 추가해주면 자동으로 android.manifest에다가 service 등록을 해줌
    [Service]
    public class ForegroundService : Service
    {
        public const string ChannelId = "notification channel";

        public bool IsStopped => _isStopped;

        private volatile bool _isStopped;

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnCreate()
        {
            Dbg.WrilteLine("서비스가 생성되었습니다.");
            MainActivity.s_ForegroundService = this;
            _isStopped = false;
        }

        public override void OnDestroy()
        {
            StopForeground(true);
            StopSelf(1);

            Dbg.WrilteLine("서비스가 삭제되었습니다.");
            _isStopped = true;

            base.OnDestroy();
        }


        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            Dbg.WrilteLine($"서비스 커맨드 수신: {intent.Action} [{startId}]");

            if (intent.Action == "startForeground")
                StartForegroundService();
            else if (intent.Action == "stopForeground")
            {
                StopForeground(true);
                StopSelf(startId);
            }

            return StartCommandResult.Sticky;
        }

        public void StartForegroundService()
        {
            NotificationCompat.Builder builder = new NotificationCompat.Builder(this, ChannelId);
            builder.SetSmallIcon(Resource.Mipmap.icon);
            builder.SetContentTitle("포그라운드 서비스 실행중");

            Intent notificationIndent = new Intent(this, typeof(MainActivity));
            PendingIntent pendingIndent = PendingIntent.GetActivity(this, 0, notificationIndent, PendingIntentFlags.Immutable);
            builder.SetContentIntent(pendingIndent);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                NotificationManager manager = (NotificationManager)GetSystemService(Service.NotificationService);
                manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "포그라운드 채널", NotificationImportance.Default));
            }

            
            Dbg.WrilteLine("서비스가 실행되었습니다.");
            StartForeground(1, builder.Build());
        }

        public void RemoveNotificationChannel()
        {
            NotificationManager manager = (NotificationManager)GetSystemService(Service.NotificationService);
            manager.DeleteNotificationChannel(ChannelId);
        }
    }
}