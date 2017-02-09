using System.Text;
using Qiniu.Common;
using Qiniu.Util;
using Qiniu.RS;
using Qiniu.RS.Model;
using Qiniu.Http;
using Newtonsoft.Json;

namespace SunSync.Models
{
    /// <summary>
    /// 批量获取(batch stat)的hash(可能612不存在)，和本地hash进行对比，确定需要上传的文件
    /// </summary>
    public class BucketFileHash
    {
        /// <summary>
        /// 批量获取文件的hash
        /// 注意:单次请求的文件数量在1000以下
        /// </summary>
        /// <param name="bktMgr"></param>
        /// <param name="bucket"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        public static string[] BatchStat(Mac mac, string bucket, string[] keys)
        {
            string[] remoteHash = new string[keys.Length];

            BucketManager bktMgr = new BucketManager(mac);

            int N = keys.Length;
            int X = 1000;
            int G = N / X;
            int M = N % X;
            int i;

            #region LOOP
            for (int g = 0; g < G; ++g)
            {
                string[] keys_1 = new string[X];
                for (i = 0; i < X; ++i)
                {
                    keys_1[i] = keys[g * X + i];
                }

                var r1 = bktMgr.BatchStat(bucket,keys_1);

                for (i = 0; i < X; ++i)
                {
                    var s1r = r1.Result[i];
                    if (s1r.Code == (int)HttpCode.OK )
                    {
                        var s = JsonConvert.DeserializeObject<StatInfo>(s1r.Data.ToString());
                        // FOUND
                        remoteHash[g * X + i] = s.Hash;
                    }
                }
            }
            #endregion LOOP

            #region RESIDUE

            string[] keys_2 = new string[M];
            for (i = 0; i < M; ++i)
            {
                keys_2[i] = keys[G * X + i];
            }

            var r2 = bktMgr.BatchStat(bucket, keys_2);

            for (i = 0; i < M; ++i)
            {
                var s2r = r2.Result[i];

                if(s2r.Code == (int)HttpCode.OK)
                {
                    var s = JsonConvert.DeserializeObject<StatInfo>(s2r.Data.ToString());
                    // FOUND
                    remoteHash[G * X + i] = s.Hash;
                }
                //if (r2[i].Code == 200)
                //{
                //    remoteHash[G * X + i] = r2[i].StatInfo.Hash;
                //}
            }

            #endregion RESIDUE

            return remoteHash;
        }                 
     
    }

    /// <summary>
    /// Batch请求返回的JSON格式字符串(数组)
    /// 以下是一个示例
    /// 
    /// [
    ///   {
    ///         "code":200,
    ///         "data":
    ///             {
    ///                 "fsize":16380,
    ///                 "hash":"FjBkn9ObUVW1Z9GvmKbbAUEp3gwE",
    ///                 "mimeType":"image/jpeg",
    ///                 "putTime":14742756456724365
    ///             }
    ///   },
    ///   {
    ///         "code":612,
    ///         "data":
    ///             {
    ///                 "error":"no such file or directory"
    ///             }
    ///   }
    /// ]
    /// </summary>
    internal class StatResponse
    {
        public int CODE  { get; set; }
        public Meta DATA { get; set; }
    }

    /// <summary>
    /// Stat的Data部分
    ///  {
    ///     "fsize":16380,
    ///     "hash":"FjBkn9ObUVW1Z9GvmKbbAUEp3gwE",
    ///     "mimeType":"image/jpeg",
    ///     "putTime":14742756456724365
    ///   }
    /// </summary>
    internal class Meta
    {
        public long fsize {get;set;}
        public string hash{get;set;}

        public string mimeType {get;set;}

        public long putTime{get;set;}
    }

    /// <summary>
    /// 待上传文件的基本信息
    /// </summary>
    public class FileItem
    {
        public string LocalFile { set; get; }
        public string SaveKey { set; get; }
        public string FileSize { get; set; }

        public long Length { get; set; }

        public string FileHash { get; set; }
        public string LastUpdate { get; set; }
    }
}
