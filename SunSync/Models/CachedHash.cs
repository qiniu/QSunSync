using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    /// <summary>
    /// 单个记录包含以下信息
    /// </summary>
    public class HashDBItem
    {
        public string LocalFile { get; set; }
        public string FileHash { get; set; }
        public string LastUpdate { get; set; }
    }

    class CachedHash
    {
        public string LocalPath { set; get; }
        public string Etag { set; get; }
        public string LastModified { set; get; }

        public static void CreateCachedHashDB(string localHashDBPath)
        {
            SQLiteConnection.CreateFile(localHashDBPath);
            string conStr = new SQLiteConnectionStringBuilder { DataSource = localHashDBPath }.ToString();
            string sqlStr = "CREATE TABLE [cached_hash] ([local_path] TEXT, [etag] CHAR(28), [last_modified] VARCHAR(50))";
            using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
            {
                sqlCon.Open();
                using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, sqlCon))
                {
                    sqlCmd.ExecuteNonQuery();
                }
                sqlCon.Close();
            }
        }

        public static CachedHash GetCachedHashByLocalPath(string localPath, SQLiteConnection localHashDB)
        {
            CachedHash cachedHash = new CachedHash();
            string querySql = "SELECT [etag], [last_modified] FROM [cached_hash] WHERE [local_path]=@local_path";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(localHashDB))
            {
                sqlCmd.CommandText = querySql;
                sqlCmd.Parameters.Add("@local_path", System.Data.DbType.String);
                sqlCmd.Parameters["@local_path"].Value = localPath;
                using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        cachedHash.LocalPath = localPath;
                        cachedHash.Etag = dr["etag"].ToString();
                        cachedHash.LastModified = dr["last_modified"].ToString();
                    }
                }
            }
            return cachedHash;
        }

        public static void UpdateCachedHash(string localPath, string etag, string lastModified, SQLiteConnection localHashDB)
        {
            string updateSql = "UPDATE [cached_hash] SET [etag]=@etag, [last_modified]=@last_modified WHERE [local_path]=@local_path";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(localHashDB))
            {
                sqlCmd.CommandText = updateSql;
                sqlCmd.Parameters.Add("@etag", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@last_modified", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@local_path", System.Data.DbType.String);
                sqlCmd.Parameters["@etag"].Value = etag;
                sqlCmd.Parameters["@last_modified"].Value = lastModified;
                sqlCmd.Parameters["@local_path"].Value = localPath;
                sqlCmd.ExecuteNonQuery();
            }
        }


        public static void InsertCachedHash(string localPath, string etag, string lastModified, SQLiteConnection localHashDB)
        {
            string insertSql = "INSERT INTO [cached_hash] ([local_path], [etag], [last_modified]) VALUES (@local_path, @etag, @last_modified)";
            using (SQLiteCommand sqlCmd = new SQLiteCommand(insertSql, localHashDB))
            {
                sqlCmd.CommandText = insertSql;
                sqlCmd.Parameters.Add("@etag", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@last_modified", System.Data.DbType.String);
                sqlCmd.Parameters.Add("@local_path", System.Data.DbType.String);
                sqlCmd.Parameters["@etag"].Value = etag;
                sqlCmd.Parameters["@last_modified"].Value = lastModified;
                sqlCmd.Parameters["@local_path"].Value = localPath;
                sqlCmd.ExecuteNonQuery();
            }
        }

        public static void InsertOrUpdateCachedHash(string localPath, string etag, string lastModified, SQLiteConnection localHashDB)
        {
            CachedHash cachedHash = GetCachedHashByLocalPath(localPath, localHashDB);
            if (!string.IsNullOrEmpty(cachedHash.LocalPath))
            {
                UpdateCachedHash(localPath, etag, lastModified, localHashDB);
            }
            else
            {
                InsertCachedHash(localPath, etag, lastModified, localHashDB);
            }
        }

        /// <summary>
        /// 批量插入/更新记录，比逐个插入/更新方式速度更快，待操作的记录越多对比越明显
        /// </summary>
        /// <param name="dbItems"></param>
        /// <param name="sqlConn"></param>
        public static void BatchInsertOrUpdate(List<FileItem> fileItems, SQLiteConnection hashDBConn)
        {
            List<string> keys = GetAllKeys(hashDBConn);
            
            int numItems = fileItems.Count;

            int nAppend = 0, nUpdate = 0;

            bool[] needUpdate = new bool[numItems];

            for(int j=0;j<numItems;++j)
            {
                string file = fileItems[j].LocalFile; 
                if(keys.Contains(file))
                {
                    needUpdate[j] = true;
                }
                else
                {
                    needUpdate[j] = false;
                }
            }

            using (DbTransaction dbTrans = hashDBConn.BeginTransaction())
            {
                using (DbCommand cmd = hashDBConn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = "INSERT INTO [cached_hash] ([local_path], [etag], [last_modified]) VALUES (@local_path, @etag, @last_modified)";
                        SQLiteParameter param0 = new SQLiteParameter("@local_path",System.Data.DbType.String);
                        SQLiteParameter param1 = new SQLiteParameter("@etag",System.Data.DbType.String);
                        SQLiteParameter param2 = new SQLiteParameter("@last_modified",System.Data.DbType.String);
                        for (int i = 0; i < fileItems.Count;++i )
                        {
                            if (needUpdate[i]) continue;

                            cmd.Parameters.Add(param0);
                            cmd.Parameters.Add(param1);
                            cmd.Parameters.Add(param2);
                            cmd.Parameters["@local_path"].Value = fileItems[i].LocalFile;
                            cmd.Parameters["@etag"].Value = fileItems[i].FileHash;
                            cmd.Parameters["@last_modified"].Value = fileItems[i].LastUpdate;
                            cmd.ExecuteNonQuery();
                            ++nAppend;
                        }

                        cmd.CommandText = "UPDATE [cached_hash] SET [etag]=@etag, [last_modified]=@last_modified WHERE [local_path]=@local_path";
                        for (int i = 0; i < fileItems.Count; ++i)
                        {
                            if (!needUpdate[i]) continue;

                            cmd.Parameters.Add(param0);
                            cmd.Parameters.Add(param1);
                            cmd.Parameters.Add(param2);
                            cmd.Parameters["@local_path"].Value = fileItems[i].LocalFile;
                            cmd.Parameters["@etag"].Value = fileItems[i].FileHash;
                            cmd.Parameters["@last_modified"].Value = fileItems[i].LastUpdate;
                            cmd.ExecuteNonQuery();
                            ++nUpdate;
                        }

                        dbTrans.Commit(); // 一次commit后完成以上全部记录插入/更新操作

                        Log.Info(string.Format("HashDB: INSERTED {0}, UPDATED {1}", nAppend, nUpdate));
                    }
                    catch(Exception)
                    {
                        dbTrans.Rollback();
                    }
                }
            }
        }

        public static List<string> GetAllKeys(SQLiteConnection hashDBConn)
        {
            List<string> keys = new List<string>();
            using (SQLiteCommand sqlCmd = new SQLiteCommand(hashDBConn))
            {
                sqlCmd.CommandText = "SELECT [local_path] FROM [cached_hash]";
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

        public static Dictionary<string,string> GetAllItems(SQLiteConnection hashDBConn)
        {
            Dictionary<string, string> itemDict = new Dictionary<string, string>();

            using (SQLiteCommand sqlCmd = new SQLiteCommand(hashDBConn))
            {
                sqlCmd.CommandText = "SELECT [local_path],[etag],[last_modified] FROM [cached_hash]";
                using (SQLiteDataReader dr = sqlCmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        string file = dr["local_path"].ToString();
                        string etag = dr["etag"].ToString();
                        itemDict.Add(file, etag);
                    }
                }
            }

            return itemDict;
        }
    }
}
