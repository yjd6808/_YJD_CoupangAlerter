using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidApp.Classes.Services.Network;
using AndroidApp.Classes.Utils;
using AndroidApp.Droid.Classes.Services.Notification;
using Xamarin.Essentials;
using Xamarin.Forms;
using Application = Android.App.Application;

namespace AndroidApp.Droid.Classes.Services
{
    [BroadcastReceiver(Enabled = true, Label = "Broadcast Receiver")]
    public class AlarmHandler : BroadcastReceiver
    {
       

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent == null) return;

            if (intent.Action == "notification")
            {
                string title = intent.GetStringExtra(AndroidNotificationManager.TitleKey);
                string message = intent.GetStringExtra(AndroidNotificationManager.MessageKey);

                AndroidNotificationManager manager = AndroidNotificationManager.Instance ?? new AndroidNotificationManager();
                manager.Show(title, message);
            }
            else if (intent.Action == "recurring alarm")
            {
                Dbg.WrilteLine("알람 도착");

            }
        }
    }
}