using Newtonsoft.Json;

namespace SunSync.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    class PutRet
    {
        [JsonProperty("hash")]
        public string Hash { set; get; }
        [JsonProperty("key")]
        public string Key { set; get; }
    }
}
