using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    public class SyncSetting
    {
        //local dir to sync
        public string SyncLocalDir { set; get; }
        //target bucket
        public string SyncTargetBucket { set; get; }
        //prefix
        public string SyncPrefix { set; get; }
        //ignore dir
        public bool IgnoreDir { set; get; }
        //overwrite same file
        public bool OverwriteFile { set; get; }
        //default chunk size
        public int DefaultChunkSize { set; get; }
        //upload threshold
        public int ChunkUploadThreshold { set; get; }
        //sync thread count
        public int SyncThreadCount { set; get; }
        //upload entry domain
        public string UploadEntryDomain { set; get; }
    }
}
