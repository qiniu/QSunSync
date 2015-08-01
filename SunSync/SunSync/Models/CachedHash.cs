using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
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

        public static string GetLocalHash(string fileFullPath, SQLiteConnection localHashDB)
        {
            string hash = "";

            //current file info
            FileInfo fileInfo = new FileInfo(fileFullPath);
            string lastModified = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();

            //cached file info
            CachedHash cachedHash = CachedHash.GetCachedHashByLocalPath(fileFullPath, localHashDB);
            string cachedEtag = cachedHash.Etag;
            string cachedLmd = cachedHash.LastModified;
            if (!string.IsNullOrEmpty(cachedEtag) && !string.IsNullOrEmpty(cachedLmd))
            {
                if (cachedLmd.Equals(lastModified))
                {
                    //file not modified
                    hash = cachedEtag;
                }
                else
                {
                    //file modified, calc the hash and update db
                    string newEtag = QETag.hash(fileFullPath);
                    hash = newEtag;
                    CachedHash.UpdateCachedHash(fileFullPath, newEtag, lastModified, localHashDB);
                }
            }
            else
            {
                //no record, calc hash and insert into db
                string newEtag = QETag.hash(fileFullPath);
                hash = newEtag;
                CachedHash.InsertCachedHash(fileFullPath, newEtag, lastModified, localHashDB);
            }

            return hash;
        }

    }
}
