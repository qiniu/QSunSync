using System;
using System.Security.Cryptography;
using System.Text;

namespace SunSync.Models
{
    class Tools
    {
        /// <summary>
        /// url safe base64 encoding
        /// </summary>
        /// <param name="str">str to encode</param>
        /// <returns>str encoded</returns>
        public static string urlsafeBase64Encode(string str)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(data, 0, data.Length).Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// md5 hash in hex string
        /// </summary>
        /// <param name="str">str to hash</param>
        /// <returns>str hashed and format in hex string</returns>
        public static string md5Hash(string str)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] data = Encoding.UTF8.GetBytes(str);
            byte[] hashData = md5.ComputeHash(data);
            StringBuilder sb = new StringBuilder(hashData.Length * 2);
            foreach (byte b in hashData)
            {
                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }
    }
}
