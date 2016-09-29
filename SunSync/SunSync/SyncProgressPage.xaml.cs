using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Data.SQLite;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncProgressPage.xaml
    /// </summary>
    public partial class SyncProgressPage : Page
    {
        private SyncSetting syncSetting;
        private MainWindow mainWindow;

        private string jobId;
        private string jobLogDir;
        private DateTime jobStart;

        private object progressLock;
        private object uploadLogLock;

        private UploadInfo[] uploadInfos;
        private object uploadInfoLock;
        //key is fileKey
        //value is datetime in milliseconds +":"+uploadedBytes
        private Dictionary<string, string> uploadedBytes;

        private object fileSkippedLock;
        private object fileExistsLock;
        private object fileOverwriteLock;
        private object fileNotOverwriteLock;
        private object fileUploadErrorLock;
        private object fileUploadSuccessLock;

        private int fileSkippedCount;
        private int fileExistsCount;
        private int fileOverwriteCount;
        private int fileNotOverwriteCount;
        private int fileUploadErrorCount;
        private int fileUploadSuccessCount;

        private StreamWriter fileSkippedWriter;
        private StreamWriter fileExistsWriter;
        private StreamWriter fileOverwriteWriter;
        private StreamWriter fileNotOverwriteWriter;
        private StreamWriter fileUploadErrorWriter;
        private StreamWriter fileUploadSuccessWriter;

        private string fileSkippedLogPath;
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

        private List<string> batchOpFiles;

        private string cacheDir;
        private string cacheFilePathDone;
        private string cacheFilePathTemp;

        private string syncLogDir;
        private string syncLogDBPath;
        private SQLiteConnection syncLogDB;

        private List<UploadItem> uploadItems = null;
        private List<HashDBItem> dbItems = null;

        private int currentIndex = 0;

        public SyncProgressPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.batchOpFiles = new List<string>();
            this.uploadedBytes = new Dictionary<string, string>();
            this.resetSyncStatus();
        }

        public void EndSetUploadIteams(List<UploadItem> uploadItems)
        {
            this.uploadItems = uploadItems;

            if (this.dbItems != null)
            {
                this.dbItems.Clear();
            }
            else
            {
                this.dbItems = new List<HashDBItem>();
            }
        }
       
        public SQLiteConnection SyncLogDB()
        {
            return this.syncLogDB;
        }

        public SQLiteConnection LocalHashDB()
        {
            return this.localHashDB;
        }

        //this is called before page loaded
        internal void LoadSyncSetting(SyncSetting syncSetting)
        {
            this.syncSetting = syncSetting;

            string jobName = string.Join("\t", new string[] { syncSetting.LocalDirectory, syncSetting.TargetBucket });
            this.jobId = Tools.md5Hash(jobName);

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            this.jobsDbPath = System.IO.Path.Combine(myDocPath, "qsunsync", "sync_jobs.db");
            this.jobLogDir = System.IO.Path.Combine(myDocPath, "qsunsync", "logs", jobId);
            this.localHashDBPath = System.IO.Path.Combine(myDocPath, "qsunsync", "local_hash.db");
            this.cacheDir = System.IO.Path.Combine(myDocPath, "qsunsync", "dircache");
            this.syncLogDir = System.IO.Path.Combine(myDocPath, "qsunsync", "synclog");
            this.syncLogDBPath = System.IO.Path.Combine(this.syncLogDir, jobId + ".log.db");

            string cacheId = jobId;
            this.cacheFilePathDone = System.IO.Path.Combine(cacheDir, cacheId + ".done");
            this.cacheFilePathTemp = System.IO.Path.Combine(cacheDir, cacheId + ".temp");
        }

        private void SyncProgressPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            //clear old sync status
            this.resetSyncStatus();

            //run new sync job
            Thread jobThread = new Thread(new ParameterizedThreadStart(this.runSyncJob));
            jobThread.Start(false);
        }

        private void resetSyncStatus()
        {
            this.doneCount = currentIndex;
            //this.totalCount = 0;
            this.progressLock = new object();
            this.uploadLogLock = new object();
            this.fileSkippedCount = 0;
            this.fileSkippedLock = new object();
            this.fileExistsCount = 0;
            this.fileExistsLock = new object();
            this.fileOverwriteCount = 0;
            this.fileOverwriteLock = new object();
            this.fileNotOverwriteCount = 0;
            this.fileNotOverwriteLock = new object();
            this.fileUploadErrorCount = 0;
            this.fileUploadErrorLock = new object();
            this.fileUploadSuccessCount = currentIndex;
            this.fileUploadSuccessLock = new object();
            this.uploadInfoLock = new object();
            this.cancelSignal = false;
            this.finishSignal = false;
            this.HaltActionButton.Content = "暂停";
            this.HaltActionButton.IsEnabled = true;
            this.ManualFinishButton.IsEnabled = false;
            this.UploadProgressTextBlock.Text = "";
            this.UploadProgressLogTextBlock.Text = "";
            ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
            this.UploadProgressDataGrid.DataContext = dataSource;
            this.batchOpFiles.Clear();
        }

        /// <summary>
        /// 更新 2016-09-22 16:40 fengyh
        /// </summary>
        private void processUpload()
        {
            try
            {
                while (currentIndex<uploadItems.Count )
                {
                    if (this.cancelSignal)
                    {
                        return;
                    }

                    int itemsLeft = uploadItems.Count-currentIndex;

                    if (itemsLeft<this.syncSetting.SyncThreadCount)
                    {
                        this.uploadFiles(itemsLeft);
                        currentIndex += itemsLeft;
                    }
                    else
                    {
                        this.uploadFiles(this.syncSetting.SyncThreadCount);
                        currentIndex += this.syncSetting.SyncThreadCount;
                    }
                }


                #region UPDATE_HASH_DB

                if (dbItems.Count > 0)
                {
                    SQLiteConnection sqlConn = null;
                    try
                    {
                        if(!File.Exists(this.localHashDBPath))
                        {
                            CachedHash.CreateCachedHashDB(this.localHashDBPath);
                        }

                        var qsb = new SQLiteConnectionStringBuilder { DataSource = this.localHashDBPath };
                        sqlConn = new SQLiteConnection(qsb.ToString());
                        sqlConn.Open();
                        CachedHash.BatchInsertOrUpdate(dbItems, sqlConn);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                    }
                    finally
                    {
                        if (sqlConn != null)
                        {
                            sqlConn.Close();
                        }
                    }
                }

                #endregion UPDATE_HASH_DB
            }
            catch (Exception ex)
            {
                Log.Fatal(ex.Message);
            }
        }

        /// <summary>
        /// 更新 2016-09-22 16:40 fengyh
        /// </summary>
        private void uploadFiles(int count)
        {
            this.uploadedBytes.Clear();
            ManualResetEvent[] doneEvents = new ManualResetEvent[count];
            this.uploadInfos = new UploadInfo[count];
            for (int taskId = 0; taskId < count; taskId++)
            {
                this.uploadInfos[taskId] = new UploadInfo();
                doneEvents[taskId] = new ManualResetEvent(false);
                FileUploader uploader = new FileUploader(this.syncSetting, doneEvents[taskId], this, taskId);
                UploadItem uploadItem = uploadItems[currentIndex + taskId];
                dbItems.Add(new HashDBItem()
                {
                    LocalFile = uploadItem.LocalFile,
                    FileHash = uploadItem.FileHash,
                    LastUpdate = uploadItem.LastUpdate
                });
                ThreadPool.QueueUserWorkItem(new WaitCallback(uploader.uploadFile), uploadItem);
            }

            try
            {
                WaitHandle.WaitAll(doneEvents);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
        }

        /// <summary>
        /// 更新 2016-09-22 16:40 fengyh
        /// </summary>
        private bool initRunJob()
        {
            bool checkOk = true;
            if (!Directory.Exists(this.jobLogDir))
            {
                try
                {
                    Directory.CreateDirectory(this.jobLogDir);
                }
                catch (Exception ex)
                {
                    checkOk = false;
                    Log.Error(string.Format("create job log dir {0} failed due to {1},", this.jobLogDir, ex.Message));
                }
            }

            if (!Directory.Exists(this.cacheDir))
            {
                try
                {
                    Directory.CreateDirectory(this.cacheDir);
                }
                catch (Exception ex)
                {
                    checkOk = false;
                    Log.Error(string.Format("create list cache dir {0} failed due to {1},", this.cacheDir, ex.Message));
                }
            }

            if (!Directory.Exists(this.syncLogDir))
            {
                try
                {
                    Directory.CreateDirectory(this.syncLogDir);
                }
                catch (Exception ex)
                {
                    checkOk = false;
                    Log.Error(string.Format("create sync log dir {0} failed due to {1},", this.syncLogDir, ex.Message));
                }
            }

            //create the upload log files
            try
            {
                this.fileSkippedLogPath = System.IO.Path.Combine(this.jobLogDir, "skipped.log");
                this.fileExistsLogPath = System.IO.Path.Combine(this.jobLogDir, "exists.log");
                this.fileOverwriteLogPath = System.IO.Path.Combine(this.jobLogDir, "overwrite.log");
                this.fileNotOverwriteLogPath = System.IO.Path.Combine(this.jobLogDir, "not_overwrite.log");
                this.fileUploadSuccessLogPath = System.IO.Path.Combine(this.jobLogDir, "success.log");
                this.fileUploadErrorLogPath = System.IO.Path.Combine(this.jobLogDir, "error.log");

                this.fileSkippedWriter = new StreamWriter(fileSkippedLogPath, false, Encoding.UTF8);
                this.fileExistsWriter = new StreamWriter(fileExistsLogPath, false, Encoding.UTF8);
                this.fileOverwriteWriter = new StreamWriter(fileOverwriteLogPath, false, Encoding.UTF8);
                this.fileNotOverwriteWriter = new StreamWriter(fileNotOverwriteLogPath, false, Encoding.UTF8);
                this.fileUploadSuccessWriter = new StreamWriter(fileUploadSuccessLogPath, false, Encoding.UTF8);
                this.fileUploadErrorWriter = new StreamWriter(fileUploadErrorLogPath, false, Encoding.UTF8);

                this.fileSkippedWriter.AutoFlush = true;
                this.fileExistsWriter.AutoFlush = true;
                this.fileOverwriteWriter.AutoFlush = true;
                this.fileNotOverwriteWriter.AutoFlush = true;
                this.fileUploadSuccessWriter.AutoFlush = true;
                this.fileUploadErrorWriter.AutoFlush = true;
            }
            catch (Exception ex)
            {
                checkOk = false;
                Log.Error(string.Format("init the log writer for job {0} failed due to {1} ", this.jobId, ex.Message));
            }

            return checkOk;
        }

        private void createOptionalDB()
        {
            //check jobs db
            if (!File.Exists(this.jobsDbPath))
            {
                try
                {
                    SyncRecord.CreateSyncRecordDB(this.jobsDbPath);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("create sync record db for job {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
            else
            {
                DateTime syncDateTime = DateTime.Now;
                try
                {
                    SyncRecord.InsertRecord(this.jobId, syncDateTime, this.syncSetting, this.jobsDbPath);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("record sync job failed for job {0} due to {1}", this.jobId, ex.Message));
                }
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

            if (!File.Exists(this.syncLogDBPath))
            {
                try
                {
                    SyncLog.CreateSyncLogDB(this.syncLogDBPath);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("create sync log db for job {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
        }

        //main job scheduler
        private void runSyncJob(object resumeObject)
        {
            bool resume = (bool)resumeObject;
            this.jobStart = DateTime.Now;
            bool checkOk = this.initRunJob();
            if (!checkOk)
            {
                this.updateUploadLog("同步发生严重错误，请查看日志信息。");
                Dispatcher.Invoke(new Action(delegate
                {
                    this.ManualFinishButton.IsEnabled = true;
                    this.HaltActionButton.IsEnabled = false;
                }));
                return;
            }
            ////create optional db
            this.createOptionalDB();
            //try
            //{
            //    //open database local hash db
            //    string conStr = new SQLiteConnectionStringBuilder { DataSource = this.localHashDBPath }.ToString();
            //    this.localHashDB = new SQLiteConnection(conStr);
            //    this.localHashDB.Open();
            //}
            //catch (Exception ex)
            //{
            //    Log.Error(string.Format("open local hash db failed due to {0}", ex.Message));
            //    if (this.localHashDB != null)
            //    {
            //        try
            //        {
            //            this.localHashDB.Close();
            //        }
            //        catch (Exception ex2)
            //        {
            //            Log.Error(string.Format("close local hash db failed due to {0}", ex2.Message));
            //        }
            //    }
            //    this.localHashDB = null;
            //}

            try
            {
                string conStr = new SQLiteConnectionStringBuilder { DataSource = this.syncLogDBPath }.ToString();
                this.syncLogDB = new SQLiteConnection(conStr);
                this.syncLogDB.Open();
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("open sync log db failed due to {0}", ex.Message));
                if (this.syncLogDB != null)
                {
                    try
                    {
                        this.syncLogDB.Close();
                    }
                    catch (Exception ex2)
                    {
                        Log.Error(string.Format("close sync log db failed due to {0}", ex2.Message));
                    }
                }
                this.syncLogDB = null;
            }

            //start job
            this.jobStart = System.DateTime.Now;
            Log.Info(string.Format("start to sync dir {0}", this.syncSetting.LocalDirectory));
            //set before run status
            this.finishSignal = false;
            this.cancelSignal = false;

            //list dirs
            string localSyncDir = syncSetting.LocalDirectory;

            if (!this.cancelSignal)
            {
                //upload
                this.updateUploadLog(string.Format("开始同步{0}下所有文件...", localSyncDir));
                this.processUpload();
            }

            //set finish signal
            this.finishSignal = true;
            this.closeLogWriters();
            if (this.localHashDB != null)
            {
                try
                {
                    this.localHashDB.Close();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("job finish close local hash db failed {0}", ex.Message));
                }
            }
            if (this.syncLogDB != null)
            {
                try
                {
                    this.syncLogDB.Close();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("job finish close sync log db failed {0}", ex.Message));
                }
            }
            if (!this.cancelSignal)
            {
                //job auto finish, jump to result page
                DateTime jobEnd = System.DateTime.Now;
                this.mainWindow.GotoSyncResultPage(this.jobId, jobEnd - this.jobStart, this.syncSetting.OverwriteDuplicate,
                    this.fileSkippedCount, this.fileSkippedLogPath,
                    this.fileExistsCount, this.fileExistsLogPath,
                    this.fileOverwriteCount, this.fileOverwriteLogPath,
                    this.fileNotOverwriteCount, this.fileNotOverwriteLogPath,
                    this.fileUploadErrorCount, this.fileUploadErrorLogPath,
                    this.fileUploadSuccessCount, this.fileUploadSuccessLogPath);
            }
        }

        //halt or resume button click
        private void HaltActionButton_EventHandler(object sender, RoutedEventArgs e)
        {
            this.HaltActionButton.IsEnabled = false;
            if (this.cancelSignal)
            {
                //reset
                this.resetSyncStatus();
                this.HaltActionButton.IsEnabled = true;
                this.HaltActionButton.Content = "暂停";
                Thread jobThread = new Thread(new ParameterizedThreadStart(this.runSyncJob));
                jobThread.Start(true);
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
                        this.HaltActionButton.IsEnabled = true;
                        this.HaltActionButton.Content = "恢复";
                        this.ManualFinishButton.IsEnabled = true;
                    }));
                }));
                checkThread.Start();
            }
        }

        //manual finish button click
        private void ManualFinishButton_EventHandler(object sender, RoutedEventArgs e)
        {
            DateTime jobEnd = System.DateTime.Now;
            this.mainWindow.GotoSyncResultPage(this.jobId, jobEnd - this.jobStart, this.syncSetting.OverwriteDuplicate,
               this.fileSkippedCount, this.fileSkippedLogPath,
               this.fileExistsCount, this.fileExistsLogPath,
               this.fileOverwriteCount, this.fileOverwriteLogPath,
               this.fileNotOverwriteCount, this.fileNotOverwriteLogPath,
               this.fileUploadErrorCount, this.fileUploadErrorLogPath,
               this.fileUploadSuccessCount, this.fileUploadSuccessLogPath);
        }

        //drop directory cache files
        private void dropcacheFilePath()
        {
            try
            {
                File.Delete(this.cacheFilePathDone);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("drop cache file {0} failed due to {1}", this.cacheFilePathDone, ex.Message));
            }
        }

        //write sync progress logs
        internal void addFileSkippedLog(string log)
        {
            lock (this.fileSkippedLock)
            {
                this.fileSkippedCount += 1;

                try
                {
                    this.fileSkippedWriter.WriteLine(log);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("write file skipped log for {0} failed due to {1}", this.jobId, ex.Message));
                }
            }
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

        //update ui status
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
                double percent = this.doneCount * 100.0 / uploadItems.Count;
                this.UploadProgressTextBlock.Text = string.Format("总上传进度: {0}/{1}, {2}%", this.doneCount, uploadItems.Count, percent.ToString("F1"));
            }));
        }

        internal void updateSingleFileProgress(int taskId, string fileFullPath, string fileKey, long fileLength, double percent)
        {
            lock (this.uploadInfoLock)
            {
                //calc
                string uploadProgress = string.Format("{0}", percent.ToString("P"));
                long newUploaded = (long)(fileLength * percent);
                TimeSpan ts = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0));
                long newMills = (long)(ts.TotalMilliseconds);

                //set
                UploadInfo uploadInfo = this.uploadInfos[taskId];
                uploadInfo.LocalPath = fileFullPath;
                uploadInfo.FileKey = fileKey;
                uploadInfo.Progress = uploadProgress;
                if (this.uploadedBytes.ContainsKey(fileKey) && string.IsNullOrEmpty(uploadInfo.FinalSpeed))
                {
                    string lastUploadInfo = this.uploadedBytes[fileKey];
                    string[] lastUploadItems = lastUploadInfo.Split(':');

                    long oldUploaded = Convert.ToInt64(lastUploadItems[0]);
                    long oldMills = Convert.ToInt64(lastUploadItems[1]);

                    long deltaBytes = newUploaded - oldUploaded;
                    long deltaMillis = newMills - oldMills;

                    if (deltaMillis > 0 && deltaBytes > 0)
                    {
                        //KB/s
                        double speed = (deltaBytes / 1.024) / deltaMillis;
                        string speedStr = "";
                        if (speed > 1024)
                        {
                            speed = speed / 1024;
                            speedStr = string.Format("{0} MB/s", speed.ToString("F1"));
                        }
                        else
                        {
                            speedStr = string.Format("{0} KB/s", speed.ToString("F1"));
                        }

                        if (newUploaded < fileLength)
                        {
                            uploadInfo.Speed = speedStr;
                        }
                        else
                        {
                            uploadInfo.Speed = speedStr;
                            uploadInfo.FinalSpeed = speedStr;
                        }
                    }
                }
                else
                {
                    this.uploadedBytes.Add(fileKey, string.Format("{0}:{1}", newUploaded, newMills));
                    uploadInfo.Speed = "---";
                }
            }
            Dispatcher.Invoke(new Action(delegate
            {
                ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
                foreach (UploadInfo info in this.uploadInfos)
                {
                    if (!string.IsNullOrEmpty(info.Progress))
                    {
                        dataSource.Add(info);
                    }
                }
                this.UploadProgressDataGrid.DataContext = dataSource;
            }));
        }

        internal bool checkCancelSignal()
        {
            return this.cancelSignal;
        }

        //close the log writers
        private void closeLogWriters()
        {
            try
            {
                if (this.fileSkippedWriter != null)
                {
                    this.fileSkippedWriter.Close();
                }
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

    }
}