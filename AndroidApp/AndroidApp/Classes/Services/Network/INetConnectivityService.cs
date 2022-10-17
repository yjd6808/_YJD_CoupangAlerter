/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 10/18/2022 3:07:44 AM
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace AndroidApp.Classes.Services.Network
{
    public interface INetConnectivityService
    {
        bool IsActivated(NetConnectivityType connected);
        List<NetConnectivityType> ActivatedNets();

        
    }
}
