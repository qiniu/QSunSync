using System;
using System.Collections.Generic;
using System.Data.Common;
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

        public static List<string> GetAllKeys(SQLiteConnection syncLogDB)
        {
            List<string> keys = new List<string>();
            using (SQLiteCommand sqlCmd = new SQLiteCommand(syncLogDB))
            {
                sqlCmd.CommandText = "SELECT [local_path] FROM [sync_log]";
                using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string file = dr["local_path"].ToString();
                        keys.Add(file);
                    }
                }
            }
            return keys;
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


        public static void BatchInsertOrUpdate(List<FileItem> items,SQLiteConnection syncLogDB)
        {
            List<string> keys = GetAllKeys(syncLogDB);

            int numItems = items.Count;

            int nAppend = 0, nUpdate = 0;

            bool[] needUpdate = new bool[numItems];

            for (int j = 0; j < numItems; ++j)
            {
                string saveKey = items[j].SaveKey;
                if (keys.Contains(saveKey))
                {
                    needUpdate[j] = true;
                }
                else
                {
                    needUpdate[j] = false;
                }
            }

            using (DbTransaction dbTrans = syncLogDB.BeginTransaction())
            {
                using (DbCommand cmd = syncLogDB.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = "INSERT INTO [sync_log] ([key], [local_path], [last_modified]) VALUES (@key, @local_path, @last_modified)";
                        SQLiteParameter param0 = new SQLiteParameter("@key", System.Data.DbType.String);
                        SQLiteParameter param1 = new SQLiteParameter("@local_path", System.Data.DbType.String);
                        SQLiteParameter param2 = new SQLiteParameter("@last_modified", System.Data.DbType.String);
                        for (int i = 0; i < items.Count; ++i)
                        {
                            if (needUpdate[i]) continue;

                            cmd.Parameters.Add(param0);
                            cmd.Parameters.Add(param1);
                            cmd.Parameters.Add(param2);
                            cmd.Parameters["@key"].Value = items[i].SaveKey;
                            cmd.Parameters["@local_path"].Value = items[i].LocalFile;
                            cmd.Parameters["@last_modified"].Value = items[i].LastUpdate;
                            cmd.ExecuteNonQuery();
                            ++nAppend;
                        }

                        cmd.CommandText = "UPDATE [sync_log] SET [local_path]=@local_path, [last_modified]=@last_modified WHERE [key]=@key";
                        for (int i = 0; i < items.Count; ++i)
                        {
                            if (!needUpdate[i]) continue;

                            cmd.Parameters.Add(param0);
                            cmd.Parameters.Add(param1);
                            cmd.Parameters.Add(param2);
                            cmd.Parameters["@key"].Value = items[i].SaveKey;
                            cmd.Parameters["@local_path"].Value = items[i].LocalFile;
                            cmd.Parameters["@last_modified"].Value = items[i].LastUpdate;
                            cmd.ExecuteNonQuery();
                            ++nUpdate;
                        }

                        dbTrans.Commit(); // 一次commit后完成以上全部记录插入/更新操作

                        Log.Info(string.Format("HashDB: INSERTED {0}, UPDATED {1}", nAppend, nUpdate));
                    }
                    catch (Exception)
                    {
                        dbTrans.Rollback();
                    }
                }
            }
        }

    }
}
