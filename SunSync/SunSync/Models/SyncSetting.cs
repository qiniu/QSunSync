using System;
using System.Data.SQLite;
using System.IO;

namespace SunSync.Models
{
    public class SyncSetting
    {
        //local directory to sync
        public string LocalDirectory { set; get; }
        
        //target bucket
        public string TargetBucket { set; get; }

        //target zone (Type.Int32 ==> Type.ZoneID)
        public int TargetZoneID { get; set; }
        
        //prefix
        public string SyncPrefix { set; get; }

        //skip prefixes
        public string SkipPrefixes { set; get; }

        //skip suffixes
        public string SkipSuffixes { set; get; }
        
        //check new files
        public bool CheckNewFiles { set; get; }
        
        //using short filename (ingnore directory path)
        // or fullname relative-path
        // 0: Full
        // 1: Relative
        // 2: Short
        public int FilenameKind { set; get; }

        //overwrite remote duplicate
        public bool OverwriteDuplicate { set; get; }
        
        //default chunk size
        public int DefaultChunkSize { set; get; }
        
        //upload threshold
        public int ChunkUploadThreshold { set; get; }
        
        //sync thread count
        public int SyncThreadCount { set; get; }

        //upload from CDN (FromCDN:true)/(Directly:false)
        public bool UploadFromCDN { set; get; }

    }

}
