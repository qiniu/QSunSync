using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using Qiniu.Util;
using Qiniu.IO;
using Qiniu.IO.Model;
using Qiniu.Http;

namespace SunSync.Models
{
    class FileUploader
    {
        private SyncSetting syncSetting;
        private ManualResetEvent doneEvent;
        private SyncProgressPage syncProgressPage;
        private int taskId;
        private SQLiteConnection localHashDB;
        private SQLiteConnection syncLogDB;

        private UploadController upController;

        public FileUploader(SyncSetting syncSetting, ManualResetEvent doneEvent, SyncProgressPage syncProgressPage, int taskId, UploadController upctl)
        {
            this.syncSetting = syncSetting;
            this.doneEvent = doneEvent;
            this.syncProgressPage = syncProgressPage;
            this.taskId = taskId;
            this.localHashDB = syncProgressPage.LocalHashDB();
            this.syncLogDB = syncProgressPage.SyncLogDB();

            upController = upctl;
        }
    
        public void uploadFile(object obj)
        {
            FileItem item = obj as FileItem;

            if (syncProgressPage.checkCancelSignal())
            {
                this.doneEvent.Set();
                return;
            }
            string fileFullPath = item.LocalFile;
            if (!File.Exists(fileFullPath))
            {
                Log.Error(string.Format("file not found error, {0}", fileFullPath));
                this.doneEvent.Set();
                return;
            }
           
            //set upload params
            int putThreshold = this.syncSetting.ChunkUploadThreshold;
            int chunkSize = this.syncSetting.DefaultChunkSize;
            bool uploadFromCDN = this.syncSetting.UploadFromCDN;

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string recordPath = System.IO.Path.Combine(myDocPath, "qsunsync", "resume");
            if (!Directory.Exists(recordPath))
            {
                Directory.CreateDirectory(recordPath);
            }

            Mac mac = new Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);

            //current file info
            FileInfo fileInfo = new FileInfo(fileFullPath);
            long fileLength = fileInfo.Length;
            string fileLastModified = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();
            //support resume upload
            string recorderKey = string.Format("{0}:{1}:{2}:{3}:{4}", this.syncSetting.LocalDirectory,
                this.syncSetting.TargetBucket, item.SaveKey, fileFullPath, fileLastModified);
            recorderKey = Hashing.CalcMD5X(recorderKey);

            this.syncProgressPage.updateUploadLog("准备上传文件 " + fileFullPath);

            PutPolicy putPolicy = new PutPolicy();
            if (this.syncSetting.OverwriteDuplicate)
            {
                putPolicy.Scope = this.syncSetting.TargetBucket + ":" + item.SaveKey;
            }
            else
            {
                putPolicy.Scope = this.syncSetting.TargetBucket;
            }
            putPolicy.SetExpires(24 * 30 * 3600);        
            
            string uptoken = Auth.CreateUploadToken(mac, putPolicy.ToJsonString());

            this.syncProgressPage.updateUploadLog("开始上传文件 " + fileFullPath);

            HttpResult result = null;

            ChunkUnit cu = (ChunkUnit)(chunkSize / (128 * 1024));

            if (item.Length > putThreshold)
            {
                ResumableUploader ru = new ResumableUploader(uploadFromCDN, cu);
                string recordFile = System.IO.Path.Combine(recordPath, Hashing.CalcMD5X(fileFullPath));

                UploadProgressHandler upph = new UploadProgressHandler(delegate (long uploaded, long total)
                {
                    this.syncProgressPage.updateSingleFileProgress(taskId, fileFullPath, item.SaveKey, uploaded, fileLength);
                });

                result = ru.UploadFile(fileFullPath, item.SaveKey, uptoken, recordFile, upph, upController);
            }
            else
            {
                FormUploader su = new FormUploader(uploadFromCDN);
                result = su.UploadFile(fileFullPath, item.SaveKey, uptoken);
            }
            
            if(result.Code == (int)HttpCode.OK)
            {
                this.syncProgressPage.updateUploadLog("上传成功 " + fileFullPath);
                this.syncProgressPage.addFileUploadSuccessLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.TargetBucket,
                        fileFullPath, item.SaveKey));
                this.syncProgressPage.updateTotalUploadProgress();
            }
            else
            {
                this.syncProgressPage.updateUploadLog("上传失败 " + fileFullPath + "，" + result.Text);
                this.syncProgressPage.addFileUploadErrorLog(string.Format("{0}\t{1}\t{2}\t{3}", this.syncSetting.TargetBucket,
                        fileFullPath, item.SaveKey, result.Text));
            }

            this.doneEvent.Set();
        }
    }
}
