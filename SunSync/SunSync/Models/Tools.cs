using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    class Tools
    {
        public static string urlsafeBase64Encode(string str)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(data, 0, data.Length).Replace('+', '-').Replace('/', '_');
        }


    }
}
