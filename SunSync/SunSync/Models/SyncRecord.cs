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
        public string SyncJobId { set; get; }
        
        public DateTime SyncDateTime { set; get; }

        public SyncSetting Settings { get; set; }

        //for display
        public string SyncDateTimeStr { set; get; }

        /// <summary>
        /// load sync settings from the database by job id
        /// </summary>
        /// <param name="jobId">job id</param>
        /// <returns>
        /// return null if not exist
        /// </returns>
        public static SyncSetting LoadSyncSettingByJobId(string jobId)
        {
            SyncSetting setting = null;
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDb = System.IO.Path.Combine(myDocPath, "qsunsync", "sync_jobs.db");

            if (File.Exists(jobsDb))
            {
                string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDb }.ToString();
                string query = "SELECT * FROM [sync_jobs]  WHERE [job_id]=@job_id";
                using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
                {
                    sqlCon.Open();
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                    {
                        sqlCmd.CommandText = query;
                        sqlCmd.Parameters.Add("@job_id", System.Data.DbType.String);
                        sqlCmd.Parameters["@job_id"].Value = jobId;
                        using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                setting = new SyncSetting();
                                setting.LocalDirectory = Convert.ToString(dr["local_directory"]);
                                setting.TargetBucket = Convert.ToString(dr["target_bucket"]);
                                setting.TargetZoneID = Convert.ToInt32(dr["target_zone_id"]);                                
                                setting.SyncPrefix = Convert.ToString(dr["sync_prefix"]);                                
                                setting.SkipPrefixes = Convert.ToString(dr["skip_prefixes"]);
                                setting.SkipSuffixes = Convert.ToString(dr["skip_suffixes"]);
                                setting.CheckNewFiles = Convert.ToBoolean(dr["check_new_files"]);
                                setting.UseShortFilename = Convert.ToBoolean(dr["use_short_filename"]);
                                setting.OverwriteDuplicate = Convert.ToBoolean(dr["overwrite_duplicate"]);
                                setting.DefaultChunkSize = Convert.ToInt32(dr["default_chunk_size"]);
                                setting.ChunkUploadThreshold = Convert.ToInt32(dr["chunk_upload_threshold"]);
                                setting.SyncThreadCount = Convert.ToInt32(dr["sync_thread_count"]);
                                setting.UploadFromCDN = Convert.ToBoolean(dr["upload_from_cdn"]);
                            }
                        }
                    }
                }
            }
            return setting;
        }

        public static void CreateSyncRecordDB(string jobsDbPath)
        {
            //create database
            SQLiteConnection.CreateFile(jobsDbPath);

            //create table
            string sqlCreate = new StringBuilder()
                .Append("CREATE TABLE [sync_jobs] (")
                .Append("[job_id] CHAR(32)  UNIQUE NOT NULL, ")
                .Append("[date_time] DATE NOT NULL, ")
                .Append("[local_directory] VARCHAR(255)  NOT NULL,")
                .Append("[target_bucket] VARCHAR(64)  NOT NULL,")
                .Append("[target_zone_id] INTEGER NOT NULL,")                
                .Append("[sync_prefix] VARCHAR(255) NOT NULL,")
                .Append("[skip_prefixes] VARCHAR(500) NOT NULL,")
                .Append("[skip_suffixes] VARCHAR(500) NOT NULL,")
                .Append("[check_new_files] BOOLEAN NOT NULL,")
                .Append("[use_short_filename] BOOLEAN NOT NULL,")
                .Append("[overwrite_duplicate] BOOLEAN NOT NULL,")                
                .Append("[default_chunk_size] INTEGER NOT NULL,")
                .Append("[chunk_upload_threshold] INTEGER NOT NULL,")
                .Append("[sync_thread_count] INTEGER NOT NULL,")
                .Append("[upload_from_cdn] BOOLEAN NOT NULL )").ToString();
           
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                {
                    sqlCmd.CommandText = sqlCreate;
                    sqlCmd.ExecuteNonQuery();
                }
                sqlCon.Close();
            }
        }

        public static void DeleteSyncJobById(string jobId, string jobsDbPath)
        {
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string deleteSql = string.Format("DELETE FROM [sync_jobs] WHERE [job_id]='{0}'", jobId);
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                {
                    sqlCmd.CommandText = deleteSql;
                    sqlCmd.ExecuteNonQuery();
                }
                sqlCon.Close();
            }
        }

        public static List<SyncRecord> LoadRecentSyncJobs(string jobsDbPath)
        {
            List<SyncRecord> syncRecords = new List<SyncRecord>();

            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string queryStr = "SELECT * FROM [sync_jobs] ORDER BY [date_time] DESC";
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(queryStr, sqlCon))
                {
                    using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            SyncRecord record = new SyncRecord() { Settings = new SyncSetting() };

                            record.SyncJobId = Convert.ToString(dr["job_id"]);
                            record.SyncDateTime = Convert.ToDateTime(dr["date_time"]);

                            record.Settings.LocalDirectory = Convert.ToString(dr["local_directory"]);
                            record.Settings.TargetBucket = Convert.ToString(dr["target_bucket"]);
                            record.Settings.TargetZoneID = Convert.ToInt32(dr["target_zone_id"]);
                            record.Settings.SyncPrefix = Convert.ToString(dr["sync_prefix"]);
                            record.Settings.SkipPrefixes = Convert.ToString(dr["skip_prefixes"]);
                            record.Settings.SkipSuffixes = Convert.ToString(dr["skip_suffixes"]);
                            record.Settings.CheckNewFiles = Convert.ToBoolean(dr["check_new_files"]);
                            record.Settings.UseShortFilename = Convert.ToBoolean(dr["use_short_filename"]);
                            record.Settings.OverwriteDuplicate = Convert.ToBoolean(dr["overwrite_duplicate"]);
                            record.Settings.DefaultChunkSize = Convert.ToInt32(dr["default_chunk_size"]);
                            record.Settings.ChunkUploadThreshold = Convert.ToInt32(dr["chunk_upload_threshold"]);
                            record.Settings.SyncThreadCount = Convert.ToInt32(dr["sync_thread_count"]);
                            record.Settings.UploadFromCDN = Convert.ToBoolean(dr["upload_from_cdn"]);
                            
                            record.SyncDateTimeStr = record.SyncDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            
                            syncRecords.Add(record);
                        }
                    }
                }
            }

            return syncRecords;
        }

        public static void InsertRecord(string jobId, DateTime dateTime, SyncSetting syncSetting, string jobsDbPath)
        {
            string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDbPath }.ToString();
            string queryDelete = "DELETE FROM [sync_jobs] WHERE [job_id]=@job_id";
            string queryInsert = new StringBuilder().Append("INSERT INTO [sync_jobs] ([job_id], [date_time], [local_directory], [target_bucket], [target_zone_id],")
                .Append("[sync_prefix], [skip_prefixes], [skip_suffixes], [check_new_files], [use_short_filename], [overwrite_duplicate],")
                .Append("[default_chunk_size], [chunk_upload_threshold], [sync_thread_count], [upload_from_cdn])")
                .Append(" VALUES ( @job_id, @date_time, @local_directory, @target_bucket, @target_zone_id, @sync_prefix, ")
                .Append("@skip_prefixes, @skip_suffixes, @check_new_files, @use_short_filename, @overwrite_duplicate, ")
                .Append("@default_chunk_size, @chunk_upload_threshold, @sync_thread_count, @upload_from_cdn )").ToString();
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteTransaction sqlTrans = sqlCon.BeginTransaction())
                {
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlCon))
                    {
                        //do delete if exists
                        sqlCmd.CommandText = queryDelete;
                        sqlCmd.Parameters.Add("@job_id", System.Data.DbType.String);

                        sqlCmd.Parameters["@job_id"].Value = jobId;
                        sqlCmd.ExecuteNonQuery();

                        //do insert
                        sqlCmd.CommandText = queryInsert;
                        sqlCmd.Parameters.Add("@job_id", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@date_time", System.Data.DbType.DateTime);
                        sqlCmd.Parameters.Add("@local_directory", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@target_bucket", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@target_zone_id", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@sync_prefix", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@skip_prefixes", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@skip_suffixes", System.Data.DbType.String);
                        sqlCmd.Parameters.Add("@check_new_files",System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@use_short_filename", System.Data.DbType.Boolean);
                        sqlCmd.Parameters.Add("@overwrite_duplicate", System.Data.DbType.Boolean);                        
                        sqlCmd.Parameters.Add("@default_chunk_size", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@chunk_upload_threshold", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@sync_thread_count", System.Data.DbType.Int32);
                        sqlCmd.Parameters.Add("@upload_from_cdn", System.Data.DbType.Boolean);

                        sqlCmd.Parameters["@job_id"].Value = jobId;
                        sqlCmd.Parameters["@date_time"].Value = dateTime;
                        sqlCmd.Parameters["@local_directory"].Value = syncSetting.LocalDirectory;
                        sqlCmd.Parameters["@target_bucket"].Value = syncSetting.TargetBucket;
                        sqlCmd.Parameters["@target_zone_id"].Value = syncSetting.TargetZoneID;
                        sqlCmd.Parameters["@sync_prefix"].Value = syncSetting.SyncPrefix;
                        sqlCmd.Parameters["@skip_prefixes"].Value = syncSetting.SkipPrefixes;
                        sqlCmd.Parameters["@skip_suffixes"].Value = syncSetting.SkipSuffixes;
                        sqlCmd.Parameters["@check_new_files"].Value = syncSetting.CheckNewFiles;
                        sqlCmd.Parameters["@use_short_filename"].Value = syncSetting.UseShortFilename;
                        sqlCmd.Parameters["@overwrite_duplicate"].Value = syncSetting.OverwriteDuplicate;
                        sqlCmd.Parameters["@default_chunk_size"].Value = syncSetting.DefaultChunkSize;
                        sqlCmd.Parameters["@chunk_upload_threshold"].Value = syncSetting.ChunkUploadThreshold;
                        sqlCmd.Parameters["@sync_thread_count"].Value = syncSetting.SyncThreadCount;
                        sqlCmd.Parameters["@upload_from_cdn"].Value = syncSetting.UploadFromCDN;                        

                        sqlCmd.ExecuteNonQuery();
                    }

                    //commit
                    sqlTrans.Commit();
                }
            }
        }
    
    }
}