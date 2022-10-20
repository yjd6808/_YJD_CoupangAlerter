/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 10/21/2022 6:49:05 AM
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using Android.Speech.Tts;

namespace AndroidApp.Classes.Services.App
{
    public interface IAppInitializer
    {
        public void SetValue(Type type, object value);
        public T GetValue<T>(Type type);
    }
}
