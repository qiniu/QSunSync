using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    [JsonObject(MemberSerialization.OptIn)]
    class SyncSetting
    {
        //local dir to sync
        [JsonProperty("sync_local_dir")]
        public string SyncLocalDir { set; get; }
        //target bucket
        [JsonProperty("sync_target_bucket")]
        public string SyncTargetBucket { set; get; }
        //prefix
        [JsonProperty("sync_prefix")]
        public string SyncPrefix { set; get; }
        //ignore dir
        [JsonProperty("ignore_dir")]
        public bool IgnoreDir { set; get; }
        //overwrite same file
        [JsonProperty("overwrite_file")]
        public bool OverwriteFile { set; get; }
        //default chunk size
        [JsonProperty("default_chunk_size")]
        public int DefaultChunkSize { set; get; }
        //upload threshold
        [JsonProperty("chunk_upload_threshold")]
        public int ChunkUploadThreshold { set; get; }
        //sync thread count
        [JsonProperty("sync_thread_count")]
        public int SyncThreadCount { set; get; }
        //upload entry domain
        [JsonProperty("upload_entry_domain")]
        public string UploadEntryDomain { set; get; }

    }
}
