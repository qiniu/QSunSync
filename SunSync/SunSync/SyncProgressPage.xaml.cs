using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Util;
using Qiniu.Storage.Model;
using System.Collections.ObjectModel;
using System.Data.SQLite;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncProgressPage.xaml
    /// </summary>
    public partial class SyncProgressPage : Page, IDisposable
    {
        private SyncSetting syncSetting;
        private MainWindow mainWindow;

        private string jobId;
        private string jobLogDir;
        private DateTime jobStart;
        private object progressLock;
        private object uploadLogLock;

        private UploadInfo[] uploadInfos;

        private object fileExistsLock;
        private object fileOverwriteLock;
        private object fileNotOverwriteLock;
        private object fileUploadErrorLock;
        private object fileUploadSuccessLock;

        private int fileExistsCount;
        private int fileOverwriteCount;
        private int fileNotOverwriteCount;
        private int fileUploadErrorCount;
        private int fileUploadSuccessCount;

        private StreamWriter fileExistsWriter;
        private StreamWriter fileOverwriteWriter;
        private StreamWriter fileNotOverwriteWriter;
        private StreamWriter fileUploadErrorWriter;
        private StreamWriter fileUploadSuccessWriter;

        private string fileExistsLogPath;
        private string fileOverwriteLogPath;
        private string fileNotOverwriteLogPath;
        private string fileUploadSuccessLogPath;
        private string fileUploadErrorLogPath;

        private bool cancelSignal;
        private bool finishSignal;

        private int doneCount;
        private int totalCount;

        private string localHashDBPath;
        private SQLiteConnection localHashDB;

        private string jobsDbPath;
        public SyncProgressPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.resetSyncProgress();
        }

        //this is called before page loaded
        internal void LoadSyncSetting(SyncSetting syncSetting)
        {
            this.syncSetting = syncSetting;

            string jobName = string.Join("\t", new string[] { syncSetting.SyncLocalDir, syncSetting.SyncTargetBucket });
            this.jobId = Tools.md5Hash(jobName);

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            this.jobsDbPath = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs.db");
            this.jobLogDir = System.IO.Path.Combine(myDocPath, "qsunbox", "logs", jobId);
            this.localHashDBPath = System.IO.Path.Combine(myDocPath, "qsunbox", "hash.db");
        }

        private void SyncProgressPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            //clear old sync status
            this.resetSyncProgress();

            //run new sync job
            Thread jobThread = new Thread(new ThreadStart(this.runSyncJob));
            jobThread.Start();
        }

        private void resetSyncProgress()
        {
            this.doneCount = 0;
            this.totalCount = 0;
            this.progressLock = new object();
            this.uploadLogLock = new object();
            this.fileExistsCount = 0;
            this.fileExistsLock = new object();
            this.fileOverwriteCount = 0;
            this.fileOverwriteLock = new object();
            this.fileNotOverwriteLock = new object();
            this.fileNotOverwriteCount = 0;
            this.fileUploadErrorLock = new object();
            this.fileUploadErrorCount = 0;
            this.fileUploadSuccessLock = new object();
            this.fileUploadSuccessCount = 0;
            this.cancelSignal = false;
            this.finishSignal = false;
            this.UploadActionButton.Content = "暂停";
            this.ManualFinishButton.IsEnabled = false;
            this.UploadProgressTextBlock.Text = "";
            this.UploadProgressLogTextBlock.Text = "";
            ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
            this.UploadProgressDataGrid.DataContext = dataSource;
        }

        internal void closeLogWriters()
        {
            try
            {
                if (this.fileExistsWriter != null)
                {
                    this.fileExistsWriter.Close();
                }
                if (this.fileOverwriteWriter != null)
                {
                    this.fileOverwriteWriter.Close();
                }
                if (this.fileNotOverwriteWriter != null)
                {
                    this.fileNotOverwriteWriter.Close();
                }
                if (this.fileUploadSuccessWriter != null)
                {
                    this.fileUploadSuccessWriter.Close();
                }
                if (this.fileUploadErrorWriter != null)
                {
                    this.fileUploadErrorWriter.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error("close log writers failed, " + ex.Message);
            }
        }

        internal string getLocalHash(string fileFullPath)
        {
            string hash = "";

            //current file info
            FileInfo fileInfo = new FileInfo(fileFullPath);
            string lastModified = fileInfo.LastWriteTimeUtc.ToFileTime().ToString();

            //cached file info
            CachedHash cachedHash = CachedHash.GetCachedHashByLocalPath(fileFullPath, this.localHashDB);
            string cachedEtag = cachedHash.Etag;
            string cachedLmd = cachedHash.LastModified;
            if (!string.IsNullOrEmpty(cachedEtag) && !string.IsNullOrEmpty(cachedLmd))
            {
                if (cachedLmd.Equals(lastModified))
                {
                    //file not modified
                    hash = cachedEtag;
                }
                else
                {
                    //file modified, calc the hash and update db
                    string newEtag = QETag.hash(fileFullPath);
                    hash = newEtag;
                    CachedHash.UpdateCachedHash(fileFullPath, newEtag, lastModified, this.localHashDB);
                }
            }
            else
            {
                //no record, calc hash and insert into db
                string newEtag = QETag.hash(fileFullPath);
                hash = newEtag;
                CachedHash.InsertCachedHash(fileFullPath, newEtag, lastModified, this.localHashDB);
            }

            return hash;
        }


        internal void processDir(string rootDir, string targetDir, List<string> fileList)
        {
            try
            {
                string[] fileEntries = Directory.GetFiles(targetDir);
                foreach (string fileName in fileEntries)
                {
                    fileList.Add(fileName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("get files from {0} failed due to {1}", targetDir, ex.Message));
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(targetDir);
                foreach (string subDir in subDirs)
                {
                    try
                    {
                        processDir(rootDir, subDir, fileList);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("directory {0} access failed due to {1}", subDir, ex.Message));
                        //todo error log
                        this.updateUploadLog("路径无法访问, " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("get dirs from {0} failed due to {1}", targetDir, ex.Message));
            }
        }


        internal void prepBeforeRunJob()
        {
            if (!Directory.Exists(this.jobLogDir))
            {
                try
                {
                    Directory.CreateDirectory(this.jobLogDir);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("create job log dir {0} failed due to {1},", this.jobLogDir, ex.Message));
                }
            }

            if (!File.Exists(this.jobsDbPath))
            {
                try
                {
                    SyncRecord.CreateSyncRecordDB(this.jobsDbPath);
                }
                catch (Exception ex)
                {
                    Log.Error("create sync record db failed, " + ex.Message);
                }
            }
            else
            {
                DateTime syncDateTime = DateTime.Now;
                try
                {
                    SyncRecord.RecordSyncJob(this.jobId, syncDateTime, this.syncSetting, this.jobsDbPath);
                }
                catch (Exception ex)
                {
                    Log.Error("record sync job failed, " + ex.Message);
                }
            }
            //create the upload log files
            try
            {
                this.fileExistsLogPath = System.IO.Path.Combine(this.jobLogDir, "exists.log");
                this.fileOverwriteLogPath = System.IO.Path.Combine(this.jobLogDir, "overwrite.log");
                this.fileNotOverwriteLogPath = System.IO.Path.Combine(this.jobLogDir, "not_overwrite.log");
                this.fileUploadSuccessLogPath = System.IO.Path.Combine(this.jobLogDir, "success.log");
                this.fileUploadErrorLogPath = System.IO.Path.Combine(this.jobLogDir, "error.log");

                this.fileExistsWriter = new StreamWriter(fileExistsLogPath, false, Encoding.UTF8);
                this.fileOverwriteWriter = new StreamWriter(fileOverwriteLogPath, false, Encoding.UTF8);
                this.fileNotOverwriteWriter = new StreamWriter(fileNotOverwriteLogPath, false, Encoding.UTF8);
                this.fileUploadSuccessWriter = new StreamWriter(fileUploadSuccessLogPath, false, Encoding.UTF8);
                this.fileUploadErrorWriter = new StreamWriter(fileUploadErrorLogPath, false, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Error("init the log writer failed, " + ex.Message);
            }

            if (!File.Exists(this.localHashDBPath))
            {
                try
                {
                    CachedHash.CreateCachedHashDB(this.localHashDBPath);
                }
                catch (Exception ex)
                {
                    Log.Error("create cached hash db failed, " + ex.Message);
                }
            }
        }
        //main job scheduler
        internal void runSyncJob()
        {
            this.prepBeforeRunJob();
            //open database
            string conStr = new SQLiteConnectionStringBuilder { DataSource = this.localHashDBPath }.ToString();
            this.localHashDB = new SQLiteConnection(conStr);
            this.localHashDB.Open();
            //start job
            this.jobStart = System.DateTime.Now;
            //set before run status
            this.finishSignal = false;
            this.cancelSignal = false;

            Dispatcher.Invoke(new Action(delegate
            {
                this.ManualFinishButton.IsEnabled = false;
            }));

            //list dirs
            string localSyncDir = syncSetting.SyncLocalDir;
            List<string> fileList = new List<string>();
            processDir(localSyncDir, localSyncDir, fileList);

            ManualResetEvent[] doneEvents = null;

            int fileCount = fileList.Count;
            this.totalCount = fileCount;
            for (int startIndex = 0; startIndex < fileCount; startIndex += this.syncSetting.SyncThreadCount)
            {
                int taskMax = this.syncSetting.SyncThreadCount;
                if (fileCount - startIndex < taskMax)
                {
                    taskMax = fileCount - startIndex;
                }
                doneEvents = new ManualResetEvent[taskMax];
                uploadInfos = new UploadInfo[taskMax];

                for (int taskId = 0; taskId < taskMax; taskId++)
                {
                    uploadInfos[taskId] = new UploadInfo();
                    doneEvents[taskId] = new ManualResetEvent(false);
                    FileUploader fu = new FileUploader(syncSetting, doneEvents[taskId], this, taskId);
                    int fileIndex = startIndex + taskId;
                    ThreadPool.QueueUserWorkItem(new WaitCallback(fu.uploadFile), fileList[fileIndex]);
                }

                //wait for all jobs to finish
                try
                {
                    WaitHandle.WaitAll(doneEvents);
                }
                catch (Exception ex)
                {
                    Log.Error("wait for all threads exit failed, " + ex.Message);
                }

                //if cancel signalled
                if (this.cancelSignal)
                {
                    break;
                }
            }

            //set finish signal
            this.finishSignal = true;
            this.closeLogWriters();
            this.localHashDB.Close();
            if (!this.cancelSignal)
            {
                //job auto finish, jump to result page
                DateTime jobEnd = System.DateTime.Now;
                this.mainWindow.GotoSyncResultPage(this.jobId, jobEnd - jobStart, this.syncSetting.OverwriteFile, this.fileExistsCount, this.fileExistsLogPath, this.fileOverwriteCount,
                this.fileOverwriteLogPath, this.fileNotOverwriteCount, this.fileNotOverwriteLogPath, this.fileUploadErrorCount, this.fileUploadErrorLogPath,
                this.fileUploadSuccessCount, this.fileUploadSuccessLogPath);
            }
        }

        internal void updateUploadLog(string log)
        {
            lock (uploadLogLock)
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    this.UploadProgressLogTextBlock.Text = log;
                }));
            }
        }

        internal void updateTotalUploadProgress()
        {
            lock (progressLock)
            {
                this.doneCount += 1;
            }
            Dispatcher.Invoke(new Action(delegate
            {
                double percent = this.doneCount * 100.0 / this.totalCount;
                this.UploadProgressTextBlock.Text = string.Format("总上传进度: {0}/{1}, {2}%", this.doneCount, this.totalCount, percent.ToString("F1"));
            }));
        }

        internal void updateSingleFileProgress()
        {
            Dispatcher.Invoke(new Action(delegate
            {
                ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
                foreach (UploadInfo uploadInfo in this.uploadInfos)
                {
                    if (!string.IsNullOrEmpty(uploadInfo.Progress))
                    {
                        dataSource.Add(uploadInfo);
                    }
                }
                this.UploadProgressDataGrid.DataContext = dataSource;
            }));
        }

        internal void addFileExistsLog(string log)
        {
            lock (this.fileExistsLock)
            {
                this.fileExistsCount += 1;

                try
                {
                    this.fileExistsWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file exists log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }

        internal void addFileOverwriteLog(string log)
        {
            lock (this.fileOverwriteLock)
            {
                this.fileOverwriteCount += 1;

                try
                {
                    this.fileOverwriteWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file overwrite log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }

        internal void addFileNotOverwriteLog(string log)
        {
            lock (this.fileNotOverwriteLock)
            {
                this.fileNotOverwriteCount += 1;
                try
                {
                    this.fileNotOverwriteWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file not overwrite log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }

        internal void addFileUploadErrorLog(string log)
        {
            lock (this.fileUploadErrorLock)
            {
                this.fileUploadErrorCount += 1;
                try
                {
                    this.fileUploadErrorWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file upload failed log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }


        internal void addFileUploadSuccessLog(string log)
        {
            lock (this.fileUploadSuccessLock)
            {
                this.fileUploadSuccessCount += 1;
                try
                {
                    this.fileUploadSuccessWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file upload success log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }

        internal bool checkCancelSignal()
        {
            return this.cancelSignal;
        }


        private void UploadActionButton_EventHandler(object sender, RoutedEventArgs e)
        {
            this.UploadActionButton.IsEnabled = false;
            if (this.cancelSignal)
            {
                //reset
                this.resetSyncProgress();
                this.UploadActionButton.IsEnabled = true;
                this.UploadActionButton.Content = "暂停";
                Thread jobThread = new Thread(new ThreadStart(this.runSyncJob));
                jobThread.Start();
            }
            else
            {
                this.cancelSignal = true;
                Thread checkThread = new Thread(new ThreadStart(delegate
                {
                    while (!this.finishSignal)
                    {
                        Thread.Sleep(1000);
                    }
                    Dispatcher.Invoke(new Action(delegate
                    {
                        this.UploadActionButton.IsEnabled = true;
                        this.UploadActionButton.Content = "恢复";
                        this.ManualFinishButton.IsEnabled = true;
                    }));
                }));
                checkThread.Start();
            }
        }

        private void ManualFinishButton_EventHandler(object sender, RoutedEventArgs e)
        {
            DateTime jobEnd = System.DateTime.Now;
            this.mainWindow.GotoSyncResultPage(this.jobId, jobEnd - jobStart, this.syncSetting.OverwriteFile, this.fileExistsCount, this.fileExistsLogPath, this.fileOverwriteCount,
               this.fileOverwriteLogPath, this.fileNotOverwriteCount, this.fileNotOverwriteLogPath, this.fileUploadErrorCount, this.fileUploadErrorLogPath,
               this.fileUploadSuccessCount, this.fileUploadSuccessLogPath);
        }

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
                string recordPath = System.IO.Path.Combine(myDocPath, "qsunbox", "record");
                if (!Directory.Exists(recordPath))
                {
                    Directory.CreateDirectory(recordPath);
                }

                //check overwrite
                Mac mac = new Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);
                BucketManager bucketManager = new BucketManager(mac);

                bool overwriteKey = false;
                //get local hash
                string localHash = this.syncProgressPage.getLocalHash(fileFullPath);
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
                uploadManger.uploadFile(fileFullPath, fileKey, uptoken, new UploadOptions(null, null, false,
                    new UpProgressHandler(delegate(string key, double percent)
                {
                    UploadInfo uploadInfo = this.syncProgressPage.uploadInfos[taskId];
                    uploadInfo.LocalPath = fileFullPath;
                    uploadInfo.FileKey = fileKey;
                    uploadInfo.Progress = string.Format("{0}", percent.ToString("P"));
                    this.syncProgressPage.updateSingleFileProgress();

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

        public void Dispose()
        {

        }
    }
}