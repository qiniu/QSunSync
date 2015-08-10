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
        public FileUploader(SyncSetting syncSetting, ManualResetEvent doneEvent, SyncProgressPage syncProgressPage, int taskId)
        {
            this.syncSetting = syncSetting;
            this.doneEvent = doneEvent;
            this.syncProgressPage = syncProgressPage;
            this.taskId = taskId;
            this.localHashDB = syncProgressPage.LocalHashDB();
        }

        public void uploadFile(object file)
        {
            if (syncProgressPage.checkCancelSignal())
            {
                this.doneEvent.Set();
                return;
            }
            string fileFullPath = file.ToString();
            if (!File.Exists(fileFullPath))
            {
                Log.Error(string.Format("file not found error, {0}", fileFullPath));
                this.doneEvent.Set();
                return;
            }
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

            bool overwriteUpload = false;
            StatResult statResult = bucketManager.stat(this.syncSetting.SyncTargetBucket, fileKey);

            if (!string.IsNullOrEmpty(statResult.Hash))
            {
                //file exists in bucket
                string localHash = "";
                //current file info
                FileInfo fileInfo = new FileInfo(fileFullPath);
                string lastModified = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();

                //cached file info
                try
                {
                    CachedHash cachedHash = CachedHash.GetCachedHashByLocalPath(fileFullPath, localHashDB);
                    string cachedEtag = cachedHash.Etag;
                    string cachedLmd = cachedHash.LastModified;
                    if (!string.IsNullOrEmpty(cachedEtag) && !string.IsNullOrEmpty(cachedLmd))
                    {
                        if (cachedLmd.Equals(lastModified))
                        {
                            //file not modified
                            localHash = cachedEtag;
                        }
                        else
                        {
                            //file modified, calc the hash and update db
                            string newEtag = QETag.hash(fileFullPath);
                            localHash = newEtag;
                            try
                            {
                                CachedHash.UpdateCachedHash(fileFullPath, newEtag, lastModified, localHashDB);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(string.Format("update local hash failed {0}", ex.Message));
                            }
                        }
                    }
                    else
                    {
                        //no record, calc hash and insert into db
                        string newEtag = QETag.hash(fileFullPath);
                        localHash = newEtag;
                        try
                        {
                            CachedHash.InsertCachedHash(fileFullPath, newEtag, lastModified, localHashDB);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("insert local hash failed {0}", ex.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("get hash from local db failed {0}", ex.Message));
                    localHash = QETag.hash(fileFullPath);
                }
                
                if (localHash.Equals(statResult.Hash))
                {
                    //same file, no need to upload
                    this.syncProgressPage.addFileExistsLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                        fileFullPath, fileKey));
                    this.syncProgressPage.updateUploadLog("空间已存在，跳过文件 " + fileFullPath);
                    this.syncProgressPage.updateTotalUploadProgress();
                    this.doneEvent.Set();
                    return;
                }
                else
                {
                    if (this.syncSetting.OverwriteFile)
                    {
                        overwriteUpload = true;
                        this.syncProgressPage.updateUploadLog("空间已存在，将覆盖 " + fileFullPath);
                    }
                    else
                    {
                        this.syncProgressPage.updateUploadLog("空间已存在，不覆盖 " + fileFullPath);
                        this.syncProgressPage.addFileNotOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                            fileFullPath, fileKey));
                        this.doneEvent.Set();
                        return;
                    }
                }
            }

            //if file not exists or need to overwrite
            this.syncProgressPage.updateUploadLog("准备上传文件 " + fileFullPath);
            UploadManager uploadManger = new UploadManager(new Qiniu.Storage.Persistent.ResumeRecorder(recordPath),
                new Qiniu.Storage.Persistent.KeyGenerator(delegate() { return recorderKey; }));
            PutPolicy putPolicy = new PutPolicy();
            if (overwriteUpload)
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
                        //write new file hash to local db
                        if (!overwriteUpload)
                        {
                            FileInfo fileInfo = new FileInfo(fileFullPath);
                            string fileLmd = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();
                            PutRet putRet = JsonConvert.DeserializeObject<PutRet>(response);
                            string fileHash = putRet.Hash;
                            if (this.localHashDB != null)
                            {
                                try
                                {
                                    CachedHash.InsertOrUpdateCachedHash(fileFullPath, fileHash, fileLmd, this.localHashDB);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(string.Format("insert or update cached hash error {0}", ex.Message));
                                }
                            }
                            Log.Debug(string.Format("insert or update qiniu hash to local: '{0}' => '{1}'", fileFullPath, fileHash));
                        }

                        //update 
                        if (overwriteUpload)
                        {
                            this.syncProgressPage.addFileOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                 fileFullPath, fileKey));
                        }
                        this.syncProgressPage.updateUploadLog("上传成功 " + fileFullPath);
                        this.syncProgressPage.addFileUploadSuccessLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                                fileFullPath, fileKey));
                        this.syncProgressPage.updateTotalUploadProgress();
                    }

                    this.doneEvent.Set();
                }));
        }
    }
}
