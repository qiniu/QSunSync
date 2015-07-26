using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Storage.Model;
using Qiniu.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SunSync.Models
{
    class FileUploader
    {
        private SyncSetting syncSetting;
        private ManualResetEvent doneEvent;
        private SyncProgressPage syncProgressPage;
        private int taskId;
        public FileUploader(SyncSetting syncSetting, ManualResetEvent doneEvent, SyncProgressPage syncProgressPage, int taskId)
        {
            this.syncSetting = syncSetting;
            this.doneEvent = doneEvent;
            this.syncProgressPage = syncProgressPage;
            this.taskId = taskId;
        }

        public void uploadFile(object file)
        {
            if (syncProgressPage.checkCancelSignal())
            {
                doneEvent.Set();
                return;
            }
            string fileFullPath = file.ToString();
            //check ignore dir
            string fileKey = "";
            if (this.syncSetting.IgnoreDir)
            {
                fileKey = System.IO.Path.GetFileName(fileFullPath);
            }
            else
            {
                string newFileFullPath = fileFullPath.Replace('\\', '/');
                string newLocalSyncDir = this.syncSetting.SyncLocalDir.Replace('\\', '/');
                int fileKeyIndex = newFileFullPath.IndexOf(newLocalSyncDir);
                fileKey = newFileFullPath.Substring(fileKeyIndex + newLocalSyncDir.Length);
                if (fileKey.StartsWith("/"))
                {
                    fileKey = fileKey.Substring(1);
                }
            }
            //add prefix
            fileKey = this.syncSetting.SyncPrefix + fileKey;
            this.syncProgressPage.updateUploadLog("准备上传文件 " + fileFullPath);
            //set upload params
            Qiniu.Common.Config.UPLOAD_HOST = this.syncSetting.UploadEntryDomain;
            Qiniu.Common.Config.UP_HOST = this.syncSetting.UploadEntryDomain;
            Qiniu.Common.Config.PUT_THRESHOLD = this.syncSetting.ChunkUploadThreshold;
            Qiniu.Common.Config.CHUNK_SIZE = this.syncSetting.DefaultChunkSize;

            //support resume upload
            string recorderKey = this.syncSetting.SyncLocalDir + ":" + this.syncSetting.SyncTargetBucket + ":" + fileKey;
            recorderKey = Tools.md5Hash(recorderKey);

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string recordPath = System.IO.Path.Combine(myDocPath, "qsunsync", "record");
            if (!Directory.Exists(recordPath))
            {
                Directory.CreateDirectory(recordPath);
            }

            //check overwrite
            Mac mac = new Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);
            BucketManager bucketManager = new BucketManager(mac);

            bool overwriteKey = false;
            //get local hash
            string localHash = CachedHash.GetLocalHash(fileFullPath, this.syncProgressPage.LocalHashDB());
            StatResult statResult = bucketManager.stat(this.syncSetting.SyncTargetBucket, fileKey);

            if (!string.IsNullOrEmpty(statResult.Hash))
            {
                //check hash 
                if (localHash.Equals(statResult.Hash))
                {
                    //same file, no need to upload
                    this.syncProgressPage.addFileExistsLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                        fileFullPath, fileKey));
                    this.syncProgressPage.updateUploadLog("空间已存在，跳过文件 " + fileFullPath);
                    this.syncProgressPage.updateTotalUploadProgress();
                    doneEvent.Set();
                    return;
                }
                else
                {
                    if (this.syncSetting.OverwriteFile)
                    {
                        overwriteKey = true;
                        this.syncProgressPage.updateUploadLog("空间已存在，将覆盖 " + fileFullPath);
                    }
                    else
                    {
                        this.syncProgressPage.updateUploadLog("空间已存在，不覆盖 " + fileFullPath);
                        this.syncProgressPage.addFileNotOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                            fileFullPath, fileKey));
                        doneEvent.Set();
                        return;
                    }
                }
            }

            //if file not exists or need to overwrite
            UploadManager uploadManger = new UploadManager(new Qiniu.Storage.Persistent.ResumeRecorder(recordPath),
                new Qiniu.Storage.Persistent.KeyGenerator(delegate() { return recorderKey; }));
            PutPolicy putPolicy = new PutPolicy();
            if (overwriteKey)
            {
                putPolicy.Scope = this.syncSetting.SyncTargetBucket + ":" + fileKey;
            }
            else
            {
                putPolicy.Scope = this.syncSetting.SyncTargetBucket;
            }
            putPolicy.SetExpires(24 * 30 * 3600);
            string uptoken = Auth.createUploadToken(putPolicy, mac);
            long fileLength = new FileInfo(fileFullPath).Length;
            this.syncProgressPage.updateUploadLog("开始上传文件 " + fileFullPath);
            uploadManger.uploadFile(fileFullPath, fileKey, uptoken, new UploadOptions(null, null, false,
                new UpProgressHandler(delegate(string key, double percent)
                {
                    string uploadProgress = string.Format("{0}", percent.ToString("P"));
                    this.syncProgressPage.updateSingleFileProgress(taskId, fileFullPath, fileKey, uploadProgress);

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
                                fileFullPath, fileKey, respInfo.Error + "" + response));
                    }
                    else
                    {
                        if (overwriteKey)
                        {
                            this.syncProgressPage.addFileOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                 fileFullPath, fileKey));
                        }
                        this.syncProgressPage.updateUploadLog("上传成功 " + fileFullPath);
                        this.syncProgressPage.addFileUploadSuccessLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                fileFullPath, fileKey));
                        this.syncProgressPage.updateTotalUploadProgress();
                    }

                    doneEvent.Set();
                }));
        }
    }
}
