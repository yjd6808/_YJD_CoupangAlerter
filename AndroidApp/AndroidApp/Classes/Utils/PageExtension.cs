/* * * * * * * * * * * * * 
 * 작성자: 윤정도
 * 생성일: 10/17/2022 7:58:47 AM
 * * * * * * * * * * * * * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Forms;

namespace AndroidApp.Classes.Utils
{
    public static class PageExtension
    {
        public static void DisplayAlert(this Page page, string content, string title = "메시지", string cancel = "확인")
        {
            page.DisplayAlert(title, content, cancel);
        }
    }
}
