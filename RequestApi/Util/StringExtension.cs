using System;
using System.Collections.Generic;
using System.Text;

namespace RequestApi.Util
{
    public static class StringExtension
    {
        public static string JoinAllNumber(this string str)
        {
            string numberStr = "";

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] >= '0' && str[i] <= '9')
                    numberStr += str[i];
            }

            return numberStr;
        }
    }
}
