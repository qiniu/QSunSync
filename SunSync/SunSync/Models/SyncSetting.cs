using System;
using System.Data.SQLite;
using System.IO;
namespace SunSync.Models
{
    public class SyncSetting
    {
        //local dir to sync
        public string SyncLocalDir { set; get; }
        //target bucket
        public string SyncTargetBucket { set; get; }
        //prefix
        public string SyncPrefix { set; get; }
        //ignore dir
        public bool IgnoreDir { set; get; }
        //overwrite same file
        public bool OverwriteFile { set; get; }
        //default chunk size
        public int DefaultChunkSize { set; get; }
        //upload threshold
        public int ChunkUploadThreshold { set; get; }
        //sync thread count
        public int SyncThreadCount { set; get; }
        //upload entry domain
        public string UploadEntryDomain { set; get; }

        /// <summary>
        /// load sync settings from the database by job id
        /// </summary>
        /// <param name="syncId">job id</param>
        /// <returns>
        /// return null if not exist
        /// </returns>
        public static SyncSetting LoadSyncSettingByJobId(string syncId)
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
                        using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                        {
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
            }
            return setting;
        }
    }
}
