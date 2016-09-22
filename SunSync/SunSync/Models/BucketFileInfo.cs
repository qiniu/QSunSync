using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Qiniu.Util;
using Qiniu.Storage;
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
        public static void BatchStat(Mac mac, string bucket, List<string> keys,List<bool> skipped, ref List<string> hashTable, ref List<long> lastModified)
        {
            BucketManager bktMgr = new BucketManager(mac);

            StringBuilder opsb = new StringBuilder();

            int k = 0;
            int n = keys.Count;
            int MAX = 1000;
            int group = (n + MAX - 1) / MAX;

            string s1 = "op=/stat/";
            string s2 = "&op=/stat/";
            for (int g = 0; g < group; ++g)
            {
                int m = MAX;
                if(g==group-1)
                {
                    m = n % MAX;
                }
                bool first = true;
                for (int i=0; i < m; ++i)
                {
                    if (skipped[k]) continue;

                    string s = s2;
                    if (first)
                    {
                        s = s1;
                        first = false;
                    }
                    opsb.Append(s + StringUtils.encodedEntry(bucket, keys[k]));
                    ++k;
                }

                HttpResult result = bktMgr.batch(opsb.ToString());

                StatResponse[] statResults = JsonConvert.DeserializeObject<StatResponse[]>(result.Response);

                for (int i = 0; i < m; ++i)
                {
                    hashTable.Add(statResults[i].DATA.hash);
                    lastModified.Add(statResults[i].DATA.putTime);
                }
            }
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
    /// “待上传文件列表”将展示以下3项信息
    /// </summary>
    public class UploadItem
    {
        public string LocalFile { set; get; }
        public string SaveKey { set; get; }
        public string FileSize { get; set; }
    }
}
