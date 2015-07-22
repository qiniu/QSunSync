using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    class Account
    {
        [JsonProperty("access_key")]
        public string AccessKey { set; get; }
        [JsonProperty("secret_key")]
        public string SecretKey { set; get; }

        /// <summary>
        /// load account settings from local file if exists
        /// 
        /// return empty object if none
        /// </summary>
        public static Account TryLoadAccount()
        {
            Account acct = new Account();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string accPath = System.IO.Path.Combine(myDocPath, "qsunbox", "account.json");
            if (File.Exists(accPath))
            {
                string accData = "";
                try
                {
                    using (StreamReader sr = new StreamReader(accPath, Encoding.UTF8))
                    {
                        accData = sr.ReadToEnd();
                    }
                    acct = JsonConvert.DeserializeObject<Account>(accData);
                }
                catch (Exception)
                {
                    //ignore the exception
                }
            }
            return acct;
        }
    }
}
