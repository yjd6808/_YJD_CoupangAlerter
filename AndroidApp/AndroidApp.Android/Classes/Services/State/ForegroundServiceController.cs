// 주기적 알람으로, 슬립모드에서 서비스 동작 유지: https://stackoverflow.com/questions/8713361/keep-a-service-running-even-when-phone-is-asleep


using System.Threading;
using Android.Content;
using Android.OS;
using AndroidApp.Classes.Services.State;
using AndroidApp.Classes.Utils;
using System.Threading.Tasks;
using Android.App;
using Android.Bluetooth;
using Java.Util;
using Application = Android.App.Application;


[assembly: Xamarin.Forms.Dependency(typeof(AndroidApp.Droid.Classes.Services.State.ForegroundServiceController))]
namespace AndroidApp.Droid.Classes.Services.State
{
    public class ForegroundServiceController : IForegroundServiceController
    {
        public void StartForegroundService()
        {
            Intent intent = new Intent(Application.Context, typeof(ForegroundService));
            intent.SetAction("startForeground");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                Application.Context.StartForegroundService(intent);
            else
                Application.Context.StartService(intent);
        }

        public Task StopForegroundServiceAsync()
        {
            ForegroundService foregroundService = MainActivity.s_ForegroundService;

            if (foregroundService != null && foregroundService.IsStopped)
                return Task.CompletedTask;

            Dbg.WrilteLine("서비스 삭제를 시작합니다.");

            foregroundService.RemoveNotificationChannel();

            var stopIntent = new Intent(Application.Context, typeof(ForegroundService));
            stopIntent.SetAction("stopForeground");
            Application.Context.StopService(stopIntent);

            
            return Task.Run(async () =>
            {
                Dbg.WrilteLine("서비스가 삭제될때까지 기다립니다.");
                while (!foregroundService.IsStopped)
                    await Task.Delay(100);

                await Xamarin.Forms.Device.InvokeOnMainThreadAsync(() => MainActivity.s_ForegroundService = null).ConfigureAwait(false);
                return true;
            });
        }

        public void StartPreventingSleep()
        {
            /* 그냥 WakeLock 써서 네트워킹 작업 시작전에 깨우고 작업완료하면 다시 WakeLock 꺼주는 식으로 구현함
               아래 로직이 잘못된 건 아니지만 내 크롤링 프로그램이 일정주기마다 알람 받는게아니라 특정 이벤트가 발생할 떄마다 필요한거기 땜에..
               WakeLock() 이걸로 기능구현함

            Intent myAlarm = new Intent(Application.Context, typeof(AlarmHandler));
            myAlarm.SetAction("recurring alarm");
            //myAlarm.putExtra("project_id", project_id); //Put Extra if needed

            PendingIntent recurringAlarm = PendingIntent.GetBroadcast(Application.Context, 0, myAlarm, PendingIntentFlags.Immutable);
            AlarmManager alarms = (AlarmManager)Application.Context.GetSystemService(Context.AlarmService);

            // AlarmManager.IntervalFifteenMinutes
            // 5분마다 알람 보내서 sleep mode 방지
            alarms.SetInexactRepeating(AlarmType.RtcWakeup, Calendar.GetInstance(TimeZone.Default).TimeInMillis + 60000, 60000, recurringAlarm); 

            */
        }

        public void StopPreventingSleep()
        {
            /*

            Intent myAlarm = new Intent(Application.Context, typeof(AlarmHandler));
            myAlarm.SetAction("recurring alarm");
            //myAlarm.putExtra("project_id",project_id); //put the SAME extras

            
            PendingIntent recurringAlarm = PendingIntent.GetBroadcast(Application.Context, 0, myAlarm, PendingIntentFlags.Immutable | PendingIntentFlags.CancelCurrent);
            AlarmManager alarms = (AlarmManager)Application.Context.GetSystemService(Context.AlarmService);
            alarms.Cancel(recurringAlarm);

            */
        }



    

        public void SendWakeupAlarm()
        {
            Intent intent = new Intent(Application.Context, typeof(AlarmHandler));
            intent.SetAction("recurring alarm");
            Application.Context.SendBroadcast(intent);
        }





        static PowerManager.WakeLock sWakeLock;
        static object _wakeUpLock = new object();
        // 무조건 메인쓰레드에서만 사용할 것
        public void WakeLock(bool state)
        {

            // https://social.msdn.microsoft.com/Forums/en-US/70c8d527-3095-46af-b232-17d4cc5956d5/how-to-wake-up-android-wear-when-it-is-in-sleep-mode?forum=xamarinandroid
            if (state)
            {
                var pm = (PowerManager)Application.Context.GetSystemService(Context.PowerService);

                if (sWakeLock == null)
                    sWakeLock = pm.NewWakeLock(WakeLockFlags.ScreenBright | WakeLockFlags.Full | WakeLockFlags.AcquireCausesWakeup, "My WakeLock Tag");

                sWakeLock.Acquire();
            }
            else
            {
                sWakeLock.Release();
            }
        }


    }
}