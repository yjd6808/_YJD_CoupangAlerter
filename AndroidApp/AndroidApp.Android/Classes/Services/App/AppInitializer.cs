// 자마린 폼 <-> 안드로이드 간의 변수를 저장하고 가져올 수 있도록 하기위함

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
using Java.Lang;
using Xamarin.Forms;

[assembly: Dependency(typeof(AndroidApp.Droid.Classes.Services.App.AppInitializer))]
namespace AndroidApp.Droid.Classes.Services.App
{
    internal class AppInitializer : IAppInitializer
    {
        public static AppInitializer s_instance = new AppInitializer();
        // ===============================================================

        private readonly Dictionary<Guid, object> _appValues = new Dictionary<Guid, object>();

        public void SetValue(Type type, object value)
        {
            if (_appValues.ContainsKey(type.GUID))
                throw new System.Exception("이미 해당 키를 가지고 있습니다.");

            _appValues.Add(type.GUID, value);
        }

        public T GetValue<T>(Type type)
        {
            if (!_appValues.ContainsKey(type.GUID))
                return default(T);

            return (T)_appValues[type.GUID];
        }

    }
}