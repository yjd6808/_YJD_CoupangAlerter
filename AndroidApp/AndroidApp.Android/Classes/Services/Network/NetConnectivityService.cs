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
using AndroidApp.Classes.Services.Network;
using Xamarin.Forms;
using Android.Net;
using Android.Net.Wifi.Aware;
using Application = Android.App.Application;

[assembly: Dependency(typeof(AndroidApp.Droid.Classes.Services.Network.NetConnectivityService))]
namespace AndroidApp.Droid.Classes.Services.Network
{
    internal class NetConnectivityService : INetConnectivityService
    {
        public static NetworkCapabilities Capabilities
        {
            get 
            {
                ConnectivityManager cm = (ConnectivityManager)Application.Context.GetSystemService(Context.ConnectivityService);
                return cm.GetNetworkCapabilities(cm.ActiveNetwork);
            }
        }


        public bool IsActivated(NetConnectivityType connected)
        {
            NetworkCapabilities capabilities = Capabilities;
            if (capabilities == null) return false;
            return capabilities.HasTransport((TransportType)connected);
        }

        public List<NetConnectivityType> ActivatedNets()
        {
            NetworkCapabilities capabilities = Capabilities;
            if (capabilities == null) return null;
            List<NetConnectivityType> activated = new List<NetConnectivityType>();

            if (capabilities.HasTransport(TransportType.Bluetooth)) activated.Add(NetConnectivityType.Bluetooth);
            if (capabilities.HasTransport(TransportType.Cellular)) activated.Add(NetConnectivityType.Cellular);
            if (capabilities.HasTransport(TransportType.Ethernet)) activated.Add(NetConnectivityType.Ethernet);
            if (capabilities.HasTransport(TransportType.Lowpan)) activated.Add(NetConnectivityType.Lowpan);
            if (capabilities.HasTransport(TransportType.Usb)) activated.Add(NetConnectivityType.Usb);
            if (capabilities.HasTransport(TransportType.Wifi)) activated.Add(NetConnectivityType.Wifi);
            if (capabilities.HasTransport(TransportType.WifiAware)) activated.Add(NetConnectivityType.WifiAware);
            if (capabilities.HasTransport(TransportType.Vpn)) activated.Add(NetConnectivityType.Vpn);
            return activated;
        }
    }
}