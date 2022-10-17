/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 10/18/2022 3:06:54 AM
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace AndroidApp.Classes.Services.Network
{
    public enum NetConnectivityType
    {
        Cellular = 0,
        Wifi = 1,
        Bluetooth = 2,
        Ethernet = 3,
        Vpn = 4,
        WifiAware = 5,
        Lowpan = 6,
        Usb = 8,
    }
}
