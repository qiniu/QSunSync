using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;

namespace SunSync.Models
{
    /// <summary>
    /// sync job record in database
    /// </summary>
    public class SyncRecord
    {
        public string SyncId { set; get; }
        public string SyncLocalDir { set; get; }
        public string SyncTargetBucket { set; get; }
        public string SyncPrefix { set; get; }
        public bool IgnoreDir { set; get; }
        public bool OverwriteFile { set; get; }
        public int DefaultChunkSize { set; get; }
        public int ChunkUploadThreshold { set; get; }
        public int SyncThreadCount { set; get; }
        public string UploadEntryDomain { set; get; }
        public DateTime SyncDateTime { set; get; }
        //for display
        public string SyncDateTimeStr { set; get; }

        public static void CreateSyncRecordDB(string jobsDbPath)
        {
            //create database
            SQLiteConnection.CreateFile(jobsDbPath);

            //create table
            string sqlCreate = new StringBuilder()
                .Append("CREATE TABLE [sync_jobs]")
                .Append("([sync_id] CHAR(32)  UNIQUE NOT NULL, ")
                .Append("[sync_local_dir] VARCHAR(255)  NOT NULL,")
                .Append("[sync_target_bucket] VARCHAR(64)  NOT NULL,")
                .Append("[sync_prefix] VARCHAR(255),")
                .Append("[ignore_dir] BOOLEAN  NULL,")
                .Append("[overwrite_file] BOOLEAN  NULL,")
                .Append("[default_chunk_size] INTEGER  NULL,")
                .Append("[chunk_upload_threshold] INTEGER  NULL,")
                .Append("[sync_thread_count] INTEGER  NULL,")
                .Append("[upload_entry_domain] VARCHAR(255)  NULL,")
                .Append("[sync_date_time] DATE  NULL )").ToString();
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                {
                    sqlCmd.CommandText = sqlCreate;
                    sqlCmd.ExecuteNonQuery();
                }
            }
        }

        public static List<SyncRecord> LoadRecentSyncJobs(string jobsDbPath)
        {
            List<SyncRecord> syncRecords = new List<SyncRecord>();

            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string queryStr = "SELECT * FROM [sync_jobs] ORDER BY [sync_date_time] DESC";
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(queryStr, sqlCon))
                {
                    using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                    {
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

        public static void RecordSyncJob(string syncId, DateTime syncDateTime, SyncSetting syncSetting, string jobsDbPath)
        {
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string queryDelete = "DELETE FROM [sync_jobs] WHERE [sync_id]=@sync_id";
            string queryInsert = new StringBuilder().Append("INSERT INTO [sync_jobs] ([sync_id], [sync_local_dir], [sync_target_bucket], ")
                .Append("[sync_prefix], [ignore_dir], [overwrite_file], [default_chunk_size], [chunk_upload_threshold], [sync_thread_count], ")
                .Append("[upload_entry_domain], [sync_date_time]) VALUES ( @sync_id, @sync_local_dir, @sync_target_bucket, @sync_prefix, @ignore_dir, ")
                .Append("@overwrite_file, @default_chunk_size, @chunk_upload_threshold, @sync_thread_count, @upload_entry_domain, @sync_date_time)").ToString();
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteTransaction sqlTrans = sqlCon.BeginTransaction())
                {
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                    {
                        //do delete if exists
                        sqlCmd.CommandText = queryDelete;
                        sqlCmd.Parameters.Add("@sync_id", System.Data.DbType.String);

                        sqlCmd.Parameters["@sync_id"].Value = syncId;
                        sqlCmd.ExecuteNonQuery();

                        //do insert
                        sqlCmd.CommandText = queryInsert;
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

                    //commit
                    sqlTrans.Commit();
                }
            }
        }
    }
}