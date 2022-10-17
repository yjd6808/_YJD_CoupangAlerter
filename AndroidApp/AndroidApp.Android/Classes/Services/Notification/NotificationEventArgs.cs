/*
 * 레퍼런스 소스코드 : https://learn.microsoft.com/en-us/xamarin/xamarin-forms/app-fundamentals/local-notifications
 */

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AndroidApp.Droid.Classes.Services.Notification
{
    public class NotificationEventArgs : EventArgs
    {
        public string Title { get; set; }
        public string Message { get; set; }
    }
}