/*
 * 레퍼런스 소스코드 : https://learn.microsoft.com/en-us/xamarin/xamarin-forms/app-fundamentals/local-notifications
 */

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using AndroidApp.Classes.Services.Notification;
using AndroidX.Core.App;
using System;
using System.Collections.Generic;
using Java.Util;

[assembly: Xamarin.Forms.Dependency(typeof(AndroidApp.Droid.Classes.Services.Notification.AndroidNotificationManager))]
namespace AndroidApp.Droid.Classes.Services.Notification
{
    public class AndroidNotificationManager : INotificationManager
    {
        private const string ChannelId = "notification channel";
        private const string ChannelName = "Default";
        private const string ChannelDescription = "The default channel for notifications.";

        public const string TitleKey = "title";
        public const string MessageKey = "message";
        public const string MesssageIdKey = "message";

        private bool _channelInitialized = false;
        private int _messageIdSeq = 0;
        private int _pendingIntentSeq = 0;

        private NotificationManager _manager;

        public event EventHandler NotificationReceived;

        public static AndroidNotificationManager Instance { get; private set; }

        public AndroidNotificationManager() => Initialize();

        public void Initialize()
        {
            if (Instance == null)
            {
                CreateNotificationChannel();
                Instance = this;
            }
        }

        public void SendNotification(string title, string message, DateTime? notifyTime = null)
        {
            if (!_channelInitialized)
            {
                CreateNotificationChannel();
            }

            if (notifyTime != null)
            {
                int msgId = _messageIdSeq++;
                Intent intent = new Intent(Application.Context, typeof(AlarmHandler));
                intent.SetAction("notification");
                intent.PutExtra(TitleKey, title);
                intent.PutExtra(MessageKey, message);
                intent.PutExtra(MesssageIdKey, msgId);

                PendingIntent pendingIntent = PendingIntent.GetBroadcast(Application.Context, _pendingIntentSeq++, intent, PendingIntentFlags.CancelCurrent);
                long triggerTime = GetNotifyTime(notifyTime.Value);
                AlarmManager alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
                alarmManager.Set(AlarmType.RtcWakeup, triggerTime, pendingIntent);

                
            }
            else
            {
                Show(title, message);
            }
        }

        public void ReceiveNotification(string title, string message)
        {
            var args = new NotificationEventArgs()
            {
                Title = title,
                Message = message,
            };
            NotificationReceived?.Invoke(null, args);
        }

        public void Show(string title, string message)
        {
            int msgId = _messageIdSeq++;
            Intent intent = new Intent(Application.Context, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.ClearTop);
            intent.SetAction("notification");
            intent.PutExtra(TitleKey, title);
            intent.PutExtra(MessageKey, message);
            intent.PutExtra(MesssageIdKey, msgId);

            PendingIntent pendingIntent = PendingIntent.GetActivity(Application.Context, _pendingIntentSeq++, intent, PendingIntentFlags.Immutable);

            NotificationCompat.Builder builder = new NotificationCompat.Builder(Application.Context, ChannelId)
                .SetContentIntent(pendingIntent)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetLargeIcon(BitmapFactory.DecodeResource(Application.Context.Resources, Resource.Mipmap.icon))
                .SetSmallIcon(Resource.Mipmap.icon)
                .SetAutoCancel(true)
                .SetDefaults((int)NotificationDefaults.Sound | (int)NotificationDefaults.Vibrate);

            Android.App.Notification notification = builder.Build();
            _manager.Notify(msgId, notification);
        }

        void CreateNotificationChannel()
        {
            _manager = (NotificationManager)Application.Context.GetSystemService(Context.NotificationService);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelNameJava = new Java.Lang.String(ChannelName);
                var channel = new NotificationChannel(ChannelId, channelNameJava, NotificationImportance.Default)
                {
                    Description = ChannelDescription
                };
                _manager.CreateNotificationChannel(channel);
            }

            _channelInitialized = true;
        }

        void RemoveNotificationChannel()
        {
            _manager.DeleteNotificationChannel(ChannelId);
            _channelInitialized = false;
        }

        long GetNotifyTime(DateTime notifyTime)
        {
            DateTime utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
            double epochDiff = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;
            long utcAlarmTime = utcTime.AddSeconds(-epochDiff).Ticks / 10000;
            return utcAlarmTime; // milliseconds
        }
    }
}