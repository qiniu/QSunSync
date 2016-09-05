using System;
using System.Collections.Generic;
using System.IO;

namespace SunSync.Models
{
    /// <summary>
    /// UploadConfig / ZONE Information
    /// 2016-08-31 16:50 [@fengyh](http://fengyh.cn/)
    /// </summary>
    /// 
    public class ZoneInfo
    {
        ///////////////////////////////////////////////////////////////////////////////////////
        // 目前暂只支持NB(CDN)/BC(CDN)
        // 2016-08-31,16:50 [@fengyh](http://fengyh.cn/)
        // 已更新 2016-09-05 18:11 [@fengyh](http://fengyh.cn/)
        // ADD NA Entry 
        /////////////////////////////////////////////////////////////////////////////////////

        #region ZONE-DICT

        private enum ZONE_ID
        {
            ZONE_NB = 0,
            ZONE_NB_CDN = 1,
            ZONE_BC = 2,
            ZONE_BC_CDN = 3,
            ZONE_NA = 4, // North America
            ZONE_NA_CDN = 5,
            //ZONE_AWS, 
            //ZONE_ABROAD_NB, 
            ZONE_UNKOWN = 65535
        };

        private List<string> UPLOAD_URL = new List<string>()
        {
            "http://up.qiniu.com",         // ZONE_NB
            "http://upload.qiniu.com",     // ZONE_NB_CDN
            "http://up-z1.qiniu.com",      // ZONE_BC
            "http://upload-z1.qiniu.com",  // ZONE_BC_CDN
            "http://up-na0.qiniu.com",    // ZONE_NA
            "http://upload-na0.qiniu.com",    // ZONE_NA_CDN
            //"http://up.gdipper.com",     // ZONE_AWS
            //"http://up.qiniug.com",      // ZONE_ABROAD_NB
            ""  // ZONE_UNKNOWN                           
        };

        private List<string> ENTRY_DOMAIN = new List<string>()
        {   
            "国内->华东机房[直传源站]",    // ZONE_NB
            "国内->华东机房[CDN加速]",     // ZONE_NB_CDN
            "国内->华北机房[直传源站]",    // ZONE_BC
            "国内->华北机房[CDN加速]",     // ZONE_BC_CDN
            "北美机房[直传源站]",    // ZONE_NA
            "北美机房[CDN加速]",     // ZONE_NA_CDN
            //"AWS",                     // ZONE_AWS
            //"海外-NB",                 // ZONE_ABROAD_NB
            "不可用"                     // ZONE_UNKNOWN
        };

        #endregion ZONE-DICT

        public List<int> GetAvailableZoneIndexes(int index)
        {
            switch (index)
            {
                case (int)ZONE_ID.ZONE_NB:
                    return new List<int>() { (int)ZONE_ID.ZONE_NB, (int)ZONE_ID.ZONE_NB_CDN };
                case (int)ZONE_ID.ZONE_NB_CDN:
                    return new List<int>() { (int)ZONE_ID.ZONE_NB_CDN, (int)ZONE_ID.ZONE_NB };
                case (int)ZONE_ID.ZONE_BC:
                    return new List<int>() { (int)ZONE_ID.ZONE_BC, (int)ZONE_ID.ZONE_BC_CDN };
                case (int)ZONE_ID.ZONE_BC_CDN:
                    return new List<int>() { (int)ZONE_ID.ZONE_BC_CDN, (int)ZONE_ID.ZONE_BC };
                case (int)ZONE_ID.ZONE_NA:
                    return new List<int>() { (int)ZONE_ID.ZONE_NA, (int)ZONE_ID.ZONE_NA_CDN };
                case (int)ZONE_ID.ZONE_NA_CDN:
                    return new List<int>() { (int)ZONE_ID.ZONE_NA_CDN, (int)ZONE_ID.ZONE_NA };
                default:
                    return null;
            }
        }

        // query available zone indexes
        public List<int> Query(string accessKey, string bucketName)
        {
            List<int> availbaleZoneIndexes = null;

            ////////////////////////////////////////////////////////////////////////////////////////
            // HTTP/GET   https://uc.qbox.me/v1/query?ak=(AK)&bucket=(Bucket)
            // 该请求的返回数据参见后面的 QueryResponse 结构
            // 2016-08-31， 16:50 [@fengyh](http://fengyh.cn/)
            ////////////////////////////////////////////////////////////////////////////////////////
            string query = string.Format("https://uc.qbox.me/v1/query?ak={0}&bucket={1}", accessKey, bucketName);

            try
            {
                System.Net.HttpWebRequest wReq = System.Net.WebRequest.Create(query) as System.Net.HttpWebRequest;
                wReq.Method = "GET";
                System.Net.HttpWebResponse wResp = wReq.GetResponse() as System.Net.HttpWebResponse;
                using (StreamReader sr = new StreamReader(wResp.GetResponseStream()))
                {
                    string respData = sr.ReadToEnd();
                    QueryResponse qr = Newtonsoft.Json.JsonConvert.DeserializeObject<QueryResponse>(respData);
                    string url = qr.HTTP.UP[0];
                    int index = UPLOAD_URL.IndexOf(url);
                    availbaleZoneIndexes = GetAvailableZoneIndexes(index);
                }
                wResp.Close();

            }
            catch (Exception ex)
            {
                throw new Exception("Error @ ZoneInfo.Qury\n" + ex.Message);
            }

            return availbaleZoneIndexes;
        }

        public List<string> GetAllEntryDomains()
        {
            return ENTRY_DOMAIN;
        }

        public List<string> GetAllUploadUrls()
        {
            return UPLOAD_URL;
        }

        public void ConfigZone(int index)
        {
            switch (index)
            {
                case (int)ZONE_ID.ZONE_NB:
                    Qiniu.Common.Config.UseZoneNB();
                    break;
                case (int)ZONE_ID.ZONE_NB_CDN:
                    Qiniu.Common.Config.UseZoneNBFromCDN();
                    break;
                case (int)ZONE_ID.ZONE_BC:
                    Qiniu.Common.Config.UseZoneBC();
                    break;
                case (int)ZONE_ID.ZONE_BC_CDN:
                    Qiniu.Common.Config.UseZoneBCFromCDN();
                    break;
                case (int)ZONE_ID.ZONE_NA:
                    Qiniu.Common.Config.UseZoneNA();
                    break;
                case (int)ZONE_ID.ZONE_NA_CDN:
                    Qiniu.Common.Config.UseZoneBCFromCDN();
                    break;
                //case (int)ZONE_ID.ZONE_AWS:
                //    Qiniu.Common.Config.UseZoneAWS();
                //    break;
                //case (int)ZONE_ID.ZONE_ABROAD_NB:
                //    Qiniu.Common.Config.UseZoneAbroadNB();
                //    break;
                default:
                    //ERROR
                    break;
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////////
        // 以下QueryResponse结构定义用于JSON解析
        // 2016-08-31, 16:50 [@fengyh](http://fengyh.cn/)
        /////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// /Response/
        /// {
        ///     "ttl" : 86400,
        ///     "http" : {
        ///         "up" : [
        ///                     "http://up.qiniu.com",
        ///                     "http://upload.qiniu.com",
        ///                     "-H up.qiniu.com http://183.136.139.16"
        ///                 ],
        ///         "io" : [
        ///                      "http://iovip.qbox.me"
        ///                 ]
        ///             },
        ///     "https" : {
        ///          "io" : [
        ///                     "https://iovip.qbox.me"
        ///                  ],
        ///         "up" : [
        ///                     "https://up.qbox.me"
        ///                  ]
        ///                  }
        /// }
        /// </summary>
        public class QueryResponse
        {
            public string TTL { get; set; }
            public HttpBulk HTTP { get; set; }

            public HttpBulk HTTPS { get; set; }
        }

        /// <summary>
        /// HttpBulk作为QueryResponse的成员
        /// </summary>
        public class HttpBulk
        {
            public string[] UP { get; set; }
            public string[] IO { get; set; }
        }
    }
}
