using Newtonsoft.Json;
using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Storage.Model;
using Qiniu.Util;
using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;

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
     
        public FileUploader(SyncSetting syncSetting, ManualResetEvent doneEvent, SyncProgressPage syncProgressPage, int taskId)
        {
            this.syncSetting = syncSetting;
            this.doneEvent = doneEvent;
            this.syncProgressPage = syncProgressPage;
            this.taskId = taskId;
            this.localHashDB = syncProgressPage.LocalHashDB();
            this.syncLogDB = syncProgressPage.SyncLogDB();
        }
    
        public void uploadFile(object uploadItem)
        {
            UploadItem item = uploadItem as UploadItem;

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
            Qiniu.Common.Config.PUT_THRESHOLD = this.syncSetting.ChunkUploadThreshold;
            Qiniu.Common.Config.CHUNK_SIZE = this.syncSetting.DefaultChunkSize;

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
            string recorderKey = string.Format("{0}:{1}:{2}:{3}:{4}", this.syncSetting.SyncLocalDir,
                this.syncSetting.SyncTargetBucket, item.SaveKey, fileFullPath, fileLastModified);
            recorderKey = Tools.md5Hash(recorderKey);

            this.syncProgressPage.updateUploadLog("准备上传文件 " + fileFullPath);
            UploadManager uploadManger = new UploadManager(new Qiniu.Storage.Persistent.ResumeRecorder(recordPath),
                new Qiniu.Storage.Persistent.KeyGenerator(delegate() { return recorderKey; }));
            PutPolicy putPolicy = new PutPolicy();
            if (this.syncSetting.OverwriteFile)
            {
                putPolicy.Scope = this.syncSetting.SyncTargetBucket + ":" + item.SaveKey;
            }
            else
            {
                putPolicy.Scope = this.syncSetting.SyncTargetBucket;
            }
            putPolicy.SetExpires(24 * 30 * 3600);
            string uptoken = Auth.createUploadToken(putPolicy, mac);

            this.syncProgressPage.updateUploadLog("开始上传文件 " + fileFullPath);
            uploadManger.uploadFile(fileFullPath, item.SaveKey, uptoken, new UploadOptions(null, null, false,
                new UpProgressHandler(delegate(string key, double percent)
                {
                    this.syncProgressPage.updateSingleFileProgress(taskId, fileFullPath, item.SaveKey, fileLength, percent);
                }), new UpCancellationSignal(delegate()
                {
                    return this.syncProgressPage.checkCancelSignal();
                }))
                , new UpCompletionHandler(delegate(string key, ResponseInfo respInfo, string response)
                {
                    if (respInfo.StatusCode != 200)
                    {
                        this.syncProgressPage.updateUploadLog("上传失败 " + fileFullPath + "，" + respInfo.Error);
                        this.syncProgressPage.addFileUploadErrorLog(string.Format("{0}\t{1}\t{2}\t{3}", this.syncSetting.SyncTargetBucket,
                                fileFullPath, item.SaveKey, respInfo.Error + "" + response));

                        //file exists error
                        if (respInfo.StatusCode == 614)
                        {
                            this.syncProgressPage.updateUploadLog("空间已存在，未覆盖 " + fileFullPath);
                            this.syncProgressPage.addFileNotOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                fileFullPath, item.SaveKey));
                        }
                    }
                    else
                    {
                        //insert or update sync log for file
                        try
                        {
                            SyncLog.InsertOrUpdateSyncLog(item.SaveKey, fileFullPath, fileLastModified, this.syncLogDB);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("insert ot update sync log error {0}", ex.Message));
                        }                                            

                        //update 
                        if (this.syncSetting.OverwriteFile)
                        {
                            this.syncProgressPage.addFileOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                 fileFullPath, item.SaveKey));
                        }
                        this.syncProgressPage.updateUploadLog("上传成功 " + fileFullPath);
                        this.syncProgressPage.addFileUploadSuccessLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                fileFullPath, item.SaveKey));
                        this.syncProgressPage.updateTotalUploadProgress();
                    }

                    this.doneEvent.Set();
                }));

        }
    }
}
