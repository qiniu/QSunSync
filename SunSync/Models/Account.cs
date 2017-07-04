using Newtonsoft.Json;
using System;
using System.IO;
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
            string accPath = Tools.getAppFile("account.json");
            if (File.Exists(accPath))
            {
                string accData = "";
                using (StreamReader sr = new StreamReader(accPath, Encoding.UTF8))
                {
                    accData = sr.ReadToEnd();
                }
                try
                {
                    acct = JsonConvert.DeserializeObject<Account>(accData);
                    Log.Info("try load account, parse account info success");
                }
                catch (Exception ex)
                {
                    Log.Error("try load account, parse account info failed, " + ex.Message);
                }
            }
            return acct;
        }
    }
}