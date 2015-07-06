using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
    }
}
