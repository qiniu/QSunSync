using Newtonsoft.Json;
using Qiniu.Http;
using Qiniu.Util;
using System;
using System.Data.SQLite;
using System.IO;
using Qiniu.Storage;
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
            //check skipped rules
            int fileRelativePathIndex = fileFullPath.IndexOf(this.syncSetting.SyncLocalDir);
            string fileRelativePath = fileFullPath.Substring(fileRelativePathIndex + this.syncSetting.SyncLocalDir.Length);
            if (fileRelativePath.StartsWith("\\"))
            {
                fileRelativePath = fileRelativePath.Substring(1);
            }

            string[] skippedPrefixes = this.syncSetting.SkipPrefixes.Split(',');

            foreach (string prefix in skippedPrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    if (fileRelativePath.StartsWith(prefix.Trim()))
                    {
                        //skip by prefix
                        this.syncProgressPage.addFileSkippedLog(string.Format("{0}\t{1}", this.syncSetting.SyncTargetBucket,
                                fileFullPath));
                        this.syncProgressPage.updateUploadLog("按照前缀规则跳过文件不同步 " + fileFullPath);
                        this.syncProgressPage.updateTotalUploadProgress();
                        this.doneEvent.Set();
                        return;
                    }
                }
            }

            string[] skippedSuffixes = this.syncSetting.SkipSuffixes.Split(',');
            foreach (string suffix in skippedSuffixes)
            {
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    if (fileRelativePath.EndsWith(suffix.Trim()))
                    {
                        //skip by suffix
                        this.syncProgressPage.addFileSkippedLog(string.Format("{0}\t{1}", this.syncSetting.SyncTargetBucket,
                               fileFullPath));
                        this.syncProgressPage.updateUploadLog("按照后缀规则跳过文件不同步 " + fileFullPath);
                        this.syncProgressPage.updateTotalUploadProgress();
                        this.doneEvent.Set();
                        return;
                    }
                }
            }

            //generate the file key
            string fileKey = "";
            if (this.syncSetting.IgnoreDir)
            {
                //check ignore dir
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

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string recordPath = System.IO.Path.Combine(myDocPath, "qsunsync", "resume");
            if (!Directory.Exists(recordPath))
            {
                Directory.CreateDirectory(recordPath);
            }

            bool overwriteUpload = false;
            Mac mac = new Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);

            //current file info
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(fileFullPath);
            long fileLength = fileInfo.Length;
            string fileLastModified = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();
            //support resume upload
            string recorderKey = string.Format("{0}:{1}:{2}:{3}:{4}", this.syncSetting.SyncLocalDir,
                this.syncSetting.SyncTargetBucket, fileKey, fileFullPath, fileLastModified);
            recorderKey = Tools.md5Hash(recorderKey);

            if (syncSetting.CheckRemoteDuplicate)
            {
                //check remotely
                BucketManager bucketManager = new BucketManager(mac, new Config());
                StatResult statResult = bucketManager.Stat(this.syncSetting.SyncTargetBucket, fileKey);

                if (statResult.Result != null && !string.IsNullOrEmpty(statResult.Result.Hash))
                {
                    //file exists in bucket
                    string localHash = "";
                    //cached file info
                    try
                    {
                        CachedHash cachedHash = CachedHash.GetCachedHashByLocalPath(fileFullPath, localHashDB);
                        string cachedEtag = cachedHash.Etag;
                        string cachedLmd = cachedHash.LastModified;
                        if (!string.IsNullOrEmpty(cachedEtag) && !string.IsNullOrEmpty(cachedLmd))
                        {
                            if (cachedLmd.Equals(fileLastModified))
                            {
                                //file not modified
                                localHash = cachedEtag;
                            }
                            else
                            {
                                //file modified, calc the hash and update db
                                string newEtag = Qiniu.Util.ETag.CalcHash(fileFullPath);
                                localHash = newEtag;
                                try
                                {
                                    CachedHash.UpdateCachedHash(fileFullPath, newEtag, fileLastModified, localHashDB);
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
                            string newEtag = Qiniu.Util.ETag.CalcHash(fileFullPath);
                            localHash = newEtag;
                            try
                            {
                                CachedHash.InsertCachedHash(fileFullPath, newEtag, fileLastModified, localHashDB);
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
                        localHash = Qiniu.Util.ETag.CalcHash(fileFullPath);
                    }

                    if (localHash.Equals(statResult.Result.Hash))
                    {
                        //same file, no need to upload
                        this.syncProgressPage.addFileExistsLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                            fileFullPath, fileKey));
                        this.syncProgressPage.updateUploadLog("空间已存在，跳过文件 " + fileFullPath);
                        this.syncProgressPage.updateTotalUploadProgress();

                        //compatible, insert or update sync log for file
                        try
                        {
                            SyncLog.InsertOrUpdateSyncLog(fileKey, fileFullPath, fileLastModified, this.syncLogDB);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("insert ot update sync log error {0}", ex.Message));
                        }
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
                            this.syncProgressPage.updateTotalUploadProgress();
                            this.doneEvent.Set();
                            return;
                        }
                    }
                }
            }
            else
            {
                //check locally
                try
                {
                    SyncLog syncLog = SyncLog.GetSyncLogByKey(fileKey, this.syncLogDB);
                    if (!string.IsNullOrEmpty(syncLog.Key))
                    {
                        //has sync log and check whether it changes
                        if (syncLog.LocalPath.Equals(fileFullPath) && syncLog.LastModified.Equals(fileLastModified))
                        {
                            this.syncProgressPage.addFileExistsLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                            fileFullPath, fileKey));
                            this.syncProgressPage.updateUploadLog("本地检查已同步，跳过" + fileFullPath);
                            this.syncProgressPage.updateTotalUploadProgress();
                            this.doneEvent.Set();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("get sync log failed {0}", ex.Message));
                    this.doneEvent.Set();
                    return;
                }

                if (this.syncSetting.OverwriteFile)
                {
                    overwriteUpload = true;
                }
            }

            //if file not exists or need to overwrite
            this.syncProgressPage.updateUploadLog("准备上传文件 " + fileFullPath);

            bool uploadByCdn = true;
            switch (this.syncSetting.UploadEntryDomain)
            {
                case 1:
                    uploadByCdn = false; break;
                default:
                    uploadByCdn = true; break;
            }

            ChunkUnit chunkSize = ChunkUnit.U4096K;
            switch (this.syncSetting.DefaultChunkSize)
            {
                case 0:
                    chunkSize = ChunkUnit.U128K; break;
                case 1:
                    chunkSize = ChunkUnit.U256K; break;
                case 2:
                    chunkSize = ChunkUnit.U512K; break;
                case 3:
                    chunkSize = ChunkUnit.U1024K; break;
                case 4:
                    chunkSize = ChunkUnit.U2048K; break;
                case 5:
                    chunkSize = ChunkUnit.U4096K; break;
                default:
                    chunkSize = ChunkUnit.U4096K; break;
            }

            string resumeFile = System.IO.Path.Combine(recordPath, recorderKey);
            Config config = new Config();
            config.ChunkSize = chunkSize;
            config.UseCdnDomains = uploadByCdn;
            config.PutThreshold = this.syncSetting.ChunkUploadThreshold;

            UploadManager uploadManager = new UploadManager(config);

            PutExtra putExtra = new PutExtra();
            putExtra.ResumeRecordFile = resumeFile;

            UploadProgressHandler progressHandler = new UploadProgressHandler(delegate (long uploadBytes, long totalBytes)
             {
                 Console.WriteLine("progress: " + uploadBytes + ", " + totalBytes);
                 double percent = uploadBytes * 1.0 / totalBytes;
                 this.syncProgressPage.updateSingleFileProgress(taskId, fileFullPath, fileKey, fileLength, percent);
             });
            putExtra.ProgressHandler = progressHandler;

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
            //set file type
            putPolicy.FileType = this.syncSetting.FileType;
            Auth auth = new Auth(mac);
            string uptoken = auth.CreateUploadToken(putPolicy.ToJsonString());

            this.syncProgressPage.updateUploadLog("开始上传文件 " + fileFullPath);

            HttpResult uploadResult = uploadManager.UploadFile(fileFullPath, fileKey, uptoken, putExtra);

            if (uploadResult.Code != 200)
            {
                this.syncProgressPage.updateUploadLog("上传失败 " + fileFullPath + "，" + uploadResult.Text);
                this.syncProgressPage.addFileUploadErrorLog(string.Format("{0}\t{1}\t{2}\t{3}", this.syncSetting.SyncTargetBucket,
                        fileFullPath, fileKey, uploadResult.Text + "" + uploadResult.RefText));

                //file exists error
                if (uploadResult.Code == 614)
                {
                    this.syncProgressPage.updateUploadLog("空间已存在，未覆盖 " + fileFullPath);
                    this.syncProgressPage.addFileNotOverwriteLog(string.Format("{0}\t{1}\t{2}", this.syncSetting.SyncTargetBucket,
                        fileFullPath, fileKey));
                }
            }
            else
            {
                //insert or update sync log for file
                try
                {
                    SyncLog.InsertOrUpdateSyncLog(fileKey, fileFullPath, fileLastModified, this.syncLogDB);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("insert ot update sync log error {0}", ex.Message));
                }

                //write new file hash to local db
                if (!overwriteUpload)
                {
                    PutRet putRet = JsonConvert.DeserializeObject<PutRet>(uploadResult.Text);
                    string fileHash = putRet.Hash;
                    if (this.localHashDB != null)
                    {
                        try
                        {
                            CachedHash.InsertOrUpdateCachedHash(fileFullPath, fileHash, fileLastModified, this.localHashDB);
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
            return;
        }
    }
}
