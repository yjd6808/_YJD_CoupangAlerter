/*
 * 레퍼런스 소스코드 : https://learn.microsoft.com/en-us/xamarin/xamarin-forms/app-fundamentals/local-notifications
 */

using System;

namespace AndroidApp.Droid.Classes.Services.Notification
{
    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; set; }
        public string Message { get; set; }
    }
}