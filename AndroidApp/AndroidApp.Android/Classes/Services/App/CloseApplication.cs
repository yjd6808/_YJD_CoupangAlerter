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
using AndroidApp.Classes.Services.App;
using Xamarin.Forms;


[assembly: Dependency(typeof(AndroidApp.Droid.Classes.Services.App.CloseApplication))]
namespace AndroidApp.Droid.Classes.Services.App
{
    internal class CloseApplication : ICloseApplication
    {
        public void Close()
        {
            // https://stackoverflow.com/questions/29257929/how-to-terminate-a-xamarin-application
            Process.KillProcess(Process.MyPid());
        }
    }
}