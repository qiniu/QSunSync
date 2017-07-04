using System;
using System.Security.Cryptography;
using System.Text;

namespace SunSync.Models
{
    class Tools
    {
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

        public static string getAppFile(string subDir, string fileName)
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appFilePath = System.IO.Path.Combine(myDocPath, "qsunsync",subDir, fileName);
            return appFilePath;
        }

        public static string getAppFile(string fileName)
        {
            return getAppFile("", fileName);
        }
       
    }


}
