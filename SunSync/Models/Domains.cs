using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace SunSync.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    class Domains
    {
        [JsonProperty("up_domain")]
        public string UpDomain { set; get; }
        [JsonProperty("rs_domain")]
        public string RsDomain { set; get; }

        /// <summary>
        /// load domains settings from local file if exists
        /// 
        /// return empty object if none
        /// </summary>
        public static Domains TryLoadDomains()
        {
            Domains domains = new Domains();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string accPath = System.IO.Path.Combine(myDocPath, "qsunsync", "domains.json");
            if (File.Exists(accPath))
            {
                string domainsData = "";
                using (StreamReader sr = new StreamReader(accPath, Encoding.UTF8))
                {
                    domainsData = sr.ReadToEnd();
                }
                try
                {
                    domains = JsonConvert.DeserializeObject<Domains>(domainsData);
                }
                catch (Exception ex)
                {
                    Log.Error("parse domains info failed, " + ex.Message);
                }
            }
            return domains;
        }
    }
}
