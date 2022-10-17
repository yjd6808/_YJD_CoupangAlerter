/*
 * 레퍼런스 소스코드 : https://learn.microsoft.com/en-us/xamarin/xamarin-forms/app-fundamentals/local-notifications
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AndroidApp.Classes.Services.Notification
{
    public interface INotificationManager
    {
        event EventHandler NotificationReceived;
        void Initialize();
        void SendNotification(string title, string message, DateTime? notifyTime = null);
        void ReceiveNotification(string title, string message);
    }
}