using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WindowsApp.Classes.Utils
{
    public static class MsgBox
    {
        public static MessageBoxResult ShowTopMost( string content, string title = "메시지",
            MessageBoxButton msgBoxButton = MessageBoxButton.OK,
            MessageBoxImage msgBoxImage = MessageBoxImage.Information)
        {
            return MessageBox.Show(content, title, msgBoxButton, msgBoxImage, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
        }
    }
}
