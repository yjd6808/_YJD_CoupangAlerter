
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace AndroidApp.Classes.Services.State
{
    public interface IForegroundServiceController
    {
        void StartForegroundService();
        Task StopForegroundServiceAsync();

        void StartPreventingSleep();
        void StopPreventingSleep();
        void SendWakeupAlarm();

        void WakeLock(bool state);
    }
}