using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SunSync.Models
{
    class UploadInfo
    {
        public string LocalPath { set; get; }
        public string FileKey { set; get; }
        public string Progress { set; get; }
    }
}
