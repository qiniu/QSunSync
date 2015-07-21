using System;

namespace SunSync.Models
{
    /// <summary>
    /// sync job record in database
    /// </summary>
    public class SyncRecord
    {
        public string SyncId { set; get; }
        public string SyncLocalDir { set; get; }
        public string SyncTargetBucket { set; get; }
        public string SyncPrefix { set; get; }
        public bool IgnoreDir { set; get; }
        public bool OverwriteFile { set; get; }
        public int DefaultChunkSize { set; get; }
        public int ChunkUploadThreshold { set; get; }
        public int SyncThreadCount { set; get; }
        public string UploadEntryDomain { set; get; }
        public DateTime SyncDateTime { set; get; }
        //for display
        public string SyncDateTimeStr { set; get; }
    }
}