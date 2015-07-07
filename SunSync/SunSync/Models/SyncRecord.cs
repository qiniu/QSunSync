using System;

namespace SunSync.Models
{
    public class SyncRecord
    {
        public string FilePath { set; get; }
        public DateTime SyncDateTime { set; get; }
        public string SyncDateTimeStr { set; get; }
        public string SyncLocalDir { set; get; }
        public string SyncTargetBucket { set; get; }
    }
}