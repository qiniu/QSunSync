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
        public bool CheckRemoteDuplicate { set; get; }
        public string SyncPrefix { set; get; }
        public bool CheckNewFiles { set; get; }
        public bool IgnoreDir { set; get; }
        public string SkipPrefixes { set; get; }
        public string SkipSuffixes { set; get; }
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
                .Append("[check_remote_duplicate] BOOLEAN NOT NULL,")
                .Append("[sync_prefix] VARCHAR(255) NOT NULL,")
                .Append("[check_new_files] BOOLEAN NOT NULL,")
                .Append("[ignore_dir] BOOLEAN NOT NULL,")
                .Append("[skip_prefixes] VARCHAR(500) NOT NULL,")
                .Append("[skip_suffixes] VARCHAR(500) NOT NULL,")
                .Append("[overwrite_file] BOOLEAN NOT NULL,")
                .Append("[default_chunk_size] INTEGER NOT NULL,")
                .Append("[chunk_upload_threshold] INTEGER NOT NULL,")
                .Append("[sync_thread_count] INTEGER NOT NULL,")
                .Append("[upload_entry_domain] VARCHAR(255) NOT NULL,")
                .Append("[sync_date_time] DATE NOT NULL )").ToString();
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

        public static void DeleteSyncJobById(string syncId, string jobsDbPath)
        {
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string deleteSql = string.Format("DELETE FROM [sync_jobs] WHERE [sync_id]='{0}'", syncId);
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                {
                    sqlCmd.CommandText = deleteSql;
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
                            record.CheckRemoteDuplicate = Convert.ToBoolean(dr["check_remote_duplicate"]);
                            record.SyncTargetBucket = Convert.ToString(dr["sync_target_bucket"]);
                            record.SyncPrefix = Convert.ToString(dr["sync_prefix"]);
                            record.CheckNewFiles = Convert.ToBoolean(dr["check_new_files"]);
                            record.IgnoreDir = Convert.ToBoolean(dr["ignore_dir"]);
                            record.SkipPrefixes = Convert.ToString(dr["skip_prefixes"]);
                            record.SkipSuffixes = Convert.ToString(dr["skip_suffixes"]);
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
            string queryInsert = new StringBuilder().Append("INSERT INTO [sync_jobs] ([sync_id], [sync_local_dir], [sync_target_bucket], [check_remote_duplicate], ")
                .Append("[sync_prefix], [check_new_files], [ignore_dir], [skip_prefixes], [skip_suffixes], [overwrite_file], [default_chunk_size], [chunk_upload_threshold], [sync_thread_count], ")
                .Append("[upload_entry_domain], [sync_date_time]) VALUES ( @sync_id, @sync_local_dir, @sync_target_bucket, @check_remote_duplicate, @sync_prefix, @check_new_files, @ignore_dir, ")
                .Append("@skip_prefixes, @skip_suffixes, @overwrite_file, @default_chunk_size, @chunk_upload_threshold, @sync_thread_count, @upload_entry_domain, @sync_date_time)").ToString();
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
                        sqlCmd.Parameters.Add("@check_remote_duplicate",System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@sync_prefix", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@check_new_files",System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@ignore_dir", System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@skip_prefixes",System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@skip_suffixes", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@overwrite_file", System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@default_chunk_size", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@chunk_upload_threshold", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@sync_thread_count", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@upload_entry_domain", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@sync_date_time", System.Data.DbType.DateTime);

                        sqlCmd.Parameters["@sync_id"].Value = syncId;
                        sqlCmd.Parameters["@sync_local_dir"].Value = syncSetting.SyncLocalDir;
                        sqlCmd.Parameters["@sync_target_bucket"].Value = syncSetting.SyncTargetBucket;
                        sqlCmd.Parameters["@check_remote_duplicate"].Value = syncSetting.CheckRemoteDuplicate;
                        sqlCmd.Parameters["@sync_prefix"].Value = syncSetting.SyncPrefix;
                        sqlCmd.Parameters["@check_new_files"].Value = syncSetting.CheckNewFiles;
                        sqlCmd.Parameters["@ignore_dir"].Value = syncSetting.IgnoreDir;
                        sqlCmd.Parameters["@skip_prefixes"].Value = syncSetting.SkipPrefixes;
                        sqlCmd.Parameters["@skip_suffixes"].Value = syncSetting.SkipSuffixes;
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