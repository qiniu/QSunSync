using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    class SyncLog
    {
        //@TODO make key unique index
        public string Key { set; get; }
        public string LocalPath { set; get; }
        public string LastModified { set; get; }

        public static void CreateSyncLogDB(string syncLogDBPath)
        {
            SQLiteConnection.CreateFile(syncLogDBPath);
            string conStr = new SQLiteConnectionStringBuilder { DataSource = syncLogDBPath }.ToString();
            string sqlStr = "CREATE TABLE [sync_log] ([key] TEXT, [local_path] TEXT, [last_modified] VARCHAR(50))";
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, sqlCon))
                {
                    sqlCmd.ExecuteNonQuery();
                }
            }
        }

        public static SyncLog GetSyncLogByKey(string key, SQLiteConnection syncLogDB)
        {
            SyncLog syncLog = new SyncLog();
            string querySql = "SELECT [key], [local_path], [last_modified] FROM [sync_log] WHERE [key]=@key";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(syncLogDB))
            {
                sqlCmd.CommandText = querySql;
                sqlCmd.Parameters.Add("@key", System.Data.DbType.String);
                sqlCmd.Parameters["@key"].Value = key;
                using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        syncLog.Key = dr["key"].ToString();
                        syncLog.LocalPath = dr["local_path"].ToString();
                        syncLog.LastModified = dr["last_modified"].ToString();
                    }
                }
            }
            return syncLog;
        }

        public static void UpdateSyncLog(string key, string localPath, string lastModified, SQLiteConnection syncLogDB)
        {
            string updateSql = "UPDATE [sync_log] SET [local_path]=@local_path, [last_modified]=@last_modified WHERE [key]=@key";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(syncLogDB))
            {
                sqlCmd.CommandText = updateSql;
                sqlCmd.Parameters.Add("@local_path", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@last_modified", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@key", System.Data.DbType.String);
                sqlCmd.Parameters["@local_path"].Value = localPath;
                sqlCmd.Parameters["@last_modified"].Value = lastModified;
                sqlCmd.Parameters["@key"].Value = key;
                sqlCmd.ExecuteNonQuery();
            }
        }

        public static void InsertSyncLog(string key, string localPath, string lastModified, SQLiteConnection syncLogDB)
        {
            string insertSql = "INSERT INTO [sync_log] ([key], [local_path], [last_modified]) VALUES (@key, @local_path, @last_modified)";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(syncLogDB))
            {
                sqlCmd.CommandText = insertSql;
                sqlCmd.Parameters.Add("@key", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@local_path", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@last_modified", System.Data.DbType.String);
                sqlCmd.Parameters["@key"].Value = key;
                sqlCmd.Parameters["@local_path"].Value = localPath;
                sqlCmd.Parameters["@last_modified"].Value = lastModified;
                sqlCmd.ExecuteNonQuery();
            }
        }

        public static void InsertOrUpdateSyncLog(string key, string localPath, string lastModified, SQLiteConnection syncLogDB)
        {
            SyncLog syncLog = GetSyncLogByKey(key, syncLogDB);
            if (!string.IsNullOrEmpty(syncLog.Key))
            {
                UpdateSyncLog(key, localPath, lastModified, syncLogDB);
            }
            else
            {
                InsertSyncLog(key, localPath, lastModified, syncLogDB);
            }
        }
    }
}
