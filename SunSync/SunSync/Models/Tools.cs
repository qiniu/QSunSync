using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        /// <summary>
        /// load account settings from local file if exists
        /// </summary>
        public static Account loadAccountInfo()
        {
            Account acct = new Account();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string accPath = System.IO.Path.Combine(myDocPath, "qsunbox", "account.json");
            if (File.Exists(accPath))
            {
                string accData = "";
                try
                {
                    using (StreamReader sr = new StreamReader(accPath, Encoding.UTF8))
                    {
                        accData = sr.ReadToEnd();
                    }
                    acct = JsonConvert.DeserializeObject<Account>(accData);
                }
                catch (Exception)
                {
                    //todo error log
                }
            }
            return acct;
        }


        public static List<SyncRecord> loadRecentSyncJobs()
        {
            List<SyncRecord> syncRecords = new List<SyncRecord>();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDir = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs");
            if (Directory.Exists(jobsDir))
            {
                string[] jobNamePaths = Directory.GetFiles(jobsDir);
                foreach (string jobNamePath in jobNamePaths)
                {
                    string jobName = System.IO.Path.GetFileName(jobNamePath);
                    try
                    {
                        string[] items = Encoding.UTF8.GetString(Convert.FromBase64String(jobName)).Split('\t');
                        if (items.Length == 3)
                        {
                            string localDir = items[0];
                            string targetBucket = items[1];
                            string binaryDate = items[2];
                            DateTime syncDate = DateTime.FromBinary(Convert.ToInt64(binaryDate));
                            SyncRecord syncRecord = new SyncRecord();
                            syncRecord.FilePath = jobNamePath;
                            syncRecord.SyncLocalDir = localDir;
                            syncRecord.SyncTargetBucket = targetBucket;
                            syncRecord.SyncDateTime = syncDate;
                            syncRecord.SyncDateTimeStr = syncDate.ToString("yyy-MM-dd HH:mm:ss");
                            syncRecords.Add(syncRecord);
                        }
                    }
                    catch (Exception) { }
                }
            }
            syncRecords.Sort(new Comparison<SyncRecord>(delegate(SyncRecord a, SyncRecord b)
            {
                return (int)(b.SyncDateTime.Subtract(a.SyncDateTime).TotalSeconds);
            }));
            return syncRecords;
        }

    }
}
