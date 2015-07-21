using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
            string jobsDb = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs.db");

            if (File.Exists(jobsDb))
            {
                string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDb }.ToString();
                string queryStr = "SELECT * FROM [sync_jobs] ORDER BY [sync_date_time] DESC";
                using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
                {
                    sqlCon.Open();
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(queryStr, sqlCon))
                    {
                        SQLiteDataReader dr = sqlCmd.ExecuteReader();
                        while (dr.Read())
                        {
                            SyncRecord record = new SyncRecord();
                            record.SyncId = Convert.ToString(dr["sync_id"]);
                            record.SyncLocalDir = Convert.ToString(dr["sync_local_dir"]);
                            record.SyncTargetBucket = Convert.ToString(dr["sync_target_bucket"]);
                            record.SyncPrefix = Convert.ToString(dr["sync_prefix"]);
                            record.IgnoreDir = Convert.ToBoolean(dr["ignore_dir"]);
                            record.OverwriteFile = Convert.ToBoolean(dr["overwrite_file"]);
                            record.DefaultChunkSize = Convert.ToInt32(dr["default_chunk_size"]);
                            record.ChunkUploadThreshold = Convert.ToInt32(dr["chunk_upload_threshold"]);
                            record.SyncThreadCount = Convert.ToInt32(dr["sync_thread_count"]);
                            record.UploadEntryDomain = Convert.ToString(dr["upload_entry_domain"]);
                            record.SyncDateTime = Convert.ToDateTime(dr["sync_date_time"]);
                            record.SyncDateTimeStr = record.SyncDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            syncRecords.Add(record);
                        }
                    }
                }
            }
            return syncRecords;
        }

        public static SyncSetting loadSyncSettingByJobId(string syncId)
        {
            SyncSetting setting = null;
             string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDb = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs.db");

            if (File.Exists(jobsDb))
            {
                string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDb }.ToString();
                string query = "SELECT * FROM [sync_jobs]  WHERE [sync_id]=@sync_id";
                using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
                {
                    sqlCon.Open();
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                    {
                        sqlCmd.CommandText = query;
                        sqlCmd.Parameters.Add("@sync_id", System.Data.DbType.String);
                        sqlCmd.Parameters["@sync_id"].Value = syncId;
                        SQLiteDataReader dr = sqlCmd.ExecuteReader();
                        if (dr.Read())
                        {
                            setting = new SyncSetting();
                            setting.SyncLocalDir = Convert.ToString(dr["sync_local_dir"]);
                            setting.SyncTargetBucket = Convert.ToString(dr["sync_target_bucket"]);
                            setting.SyncPrefix = Convert.ToString(dr["sync_prefix"]);
                            setting.IgnoreDir = Convert.ToBoolean(dr["ignore_dir"]);
                            setting.OverwriteFile = Convert.ToBoolean(dr["overwrite_file"]);
                            setting.DefaultChunkSize = Convert.ToInt32(dr["default_chunk_size"]);
                            setting.ChunkUploadThreshold = Convert.ToInt32(dr["chunk_upload_threshold"]);
                            setting.SyncThreadCount = Convert.ToInt32(dr["sync_thread_count"]);
                            setting.UploadEntryDomain = Convert.ToString(dr["upload_entry_domain"]);
                        }
                    }
                }
            }
            else
            {
                //todo
            }
            return setting;
        }


        public static void recordSyncJob(string syncId, DateTime syncDateTime, SyncSetting syncSetting)
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDb = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs.db");

            if (File.Exists(jobsDb))
            {
                string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDb }.ToString();
                string query = new StringBuilder().Append("INSERT INTO [sync_jobs] ([sync_id], [sync_local_dir], [sync_target_bucket], ")
                    .Append("[sync_prefix], [ignore_dir], [overwrite_file], [default_chunk_size], [chunk_upload_threshold], [sync_thread_count], ")
                    .Append("[upload_entry_domain], [sync_date_time]) VALUES ( @sync_id, @sync_local_dir, @sync_target_bucket, @sync_prefix, @ignore_dir, ")
                    .Append("@overwrite_file, @default_chunk_size, @chunk_upload_threshold, @sync_thread_count, @upload_entry_domain, @sync_date_time)").ToString();
                using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
                {
                    sqlCon.Open();
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                    {
                        sqlCmd.CommandText = query;
                        sqlCmd.Parameters.Add("@sync_id", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@sync_local_dir", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@sync_target_bucket", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@sync_prefix", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@ignore_dir", System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@overwrite_file", System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@default_chunk_size", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@chunk_upload_threshold", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@sync_thread_count", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@upload_entry_domain", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@sync_date_time", System.Data.DbType.DateTime);

                        sqlCmd.Parameters["@sync_id"].Value = syncId;
                        sqlCmd.Parameters["@sync_local_dir"].Value = syncSetting.SyncLocalDir;
                        sqlCmd.Parameters["@sync_target_bucket"].Value = syncSetting.SyncTargetBucket;
                        sqlCmd.Parameters["@sync_prefix"].Value = syncSetting.SyncPrefix;
                        sqlCmd.Parameters["@ignore_dir"].Value = syncSetting.IgnoreDir;
                        sqlCmd.Parameters["@overwrite_file"].Value = syncSetting.OverwriteFile;
                        sqlCmd.Parameters["@default_chunk_size"].Value = syncSetting.DefaultChunkSize;
                        sqlCmd.Parameters["@chunk_upload_threshold"].Value = syncSetting.ChunkUploadThreshold;
                        sqlCmd.Parameters["@sync_thread_count"].Value = syncSetting.SyncThreadCount;
                        sqlCmd.Parameters["@upload_entry_domain"].Value = syncSetting.UploadEntryDomain;
                        sqlCmd.Parameters["@sync_date_time"].Value = syncDateTime;

                        sqlCmd.ExecuteNonQuery();
                    }
                }
            }
            else
            {
                //todo
            }
        }
    }
}
