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

        private List<string> batchOpFiles;

        public SyncProgressPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.batchOpFiles = new List<string>();
            this.resetSyncProgress();
        }

        public SQLiteConnection LocalHashDB()
        {
            return this.localHashDB;
        }

        //this is called before page loaded
        internal void LoadSyncSetting(SyncSetting syncSetting)
        {
            this.syncSetting = syncSetting;

            string jobName = string.Join("\t", new string[] { syncSetting.SyncLocalDir, syncSetting.SyncTargetBucket });
            this.jobId = Tools.md5Hash(jobName);

            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            this.jobsDbPath = System.IO.Path.Combine(myDocPath, "qsunsync", "jobs.db");
            this.jobLogDir = System.IO.Path.Combine(myDocPath, "qsunsync", "logs", jobId);
            this.localHashDBPath = System.IO.Path.Combine(myDocPath, "qsunsync", "hash.db");
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
            this.fileNotOverwriteCount = 0;
            this.fileNotOverwriteLock = new object();
            this.fileUploadErrorCount = 0;
            this.fileUploadErrorLock = new object();
            this.fileUploadSuccessCount = 0;
            this.fileUploadSuccessLock = new object();
            this.cancelSignal = false;
            this.finishSignal = false;
            this.UploadActionButton.Content = "暂停";
            this.ManualFinishButton.IsEnabled = false;
            this.UploadProgressTextBlock.Text = "";
            this.UploadProgressLogTextBlock.Text = "";
            ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
            this.UploadProgressDataGrid.DataContext = dataSource;
            this.batchOpFiles.Clear();
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

        internal void processDirCount(string rootDir, string targetDir)
        {
            try
            {
                string[] fileEntries = Directory.GetFiles(targetDir);
                foreach (string fileName in fileEntries)
                {
                    this.totalCount += 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("counting: get files from {0} failed due to {1}", targetDir, ex.Message));
            }

            if (this.cancelSignal)
            {
                return;
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(targetDir);
                foreach (string subDir in subDirs)
                {
                    if (this.cancelSignal)
                    {
                        return;
                    }
                    processDirCount(rootDir, subDir);
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("counting: get dirs from {0} failed due to {1}", targetDir, ex.Message));
            }
        }
        
        internal void processDirUpload(string rootDir, string targetDir)
        {
            try
            {
                string[] fileEntries = Directory.GetFiles(targetDir);
                foreach (string fileName in fileEntries)
                {
                    if (this.cancelSignal)
                    {
                        return;
                    }
                    if (batchOpFiles.Count < this.syncSetting.SyncThreadCount)
                    {
                        batchOpFiles.Add(fileName);
                    }
                    else
                    {
                        this.uploadFiles(batchOpFiles);
                        this.batchOpFiles.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                this.updateUploadLog(string.Format("无法获取路径 {0} 下文件列表, {1}", targetDir, ex.Message));
                Log.Error(string.Format("get files from {0} failed due to {1}", targetDir, ex.Message));
            }

            if (this.cancelSignal)
            {
                return;
            }

            try
            {
                string[] subDirs = Directory.GetDirectories(targetDir);
                foreach (string subDir in subDirs)
                {
                    if (this.cancelSignal)
                    {
                        return;
                    }
                    processDirUpload(rootDir, subDir);
                }
            }
            catch (Exception ex)
            {
                this.updateUploadLog(string.Format("无法获取路径 {0} 下文件夹列表, {1}", targetDir, ex.Message));
                Log.Error(string.Format("get dirs from {0} failed due to {1}", targetDir, ex.Message));
            }
        }


        internal void uploadFiles(List<string> filesToUpload)
        {
            ManualResetEvent[] doneEvents = null;
            int taskMax = filesToUpload.Count;
            doneEvents = new ManualResetEvent[taskMax];
            ThreadPool.SetMinThreads(taskMax, taskMax);
            this.uploadInfos = new UploadInfo[taskMax];
            for (int taskId = 0; taskId < taskMax; taskId++)
            {
                this.uploadInfos[taskId] = new UploadInfo();
                doneEvents[taskId] = new ManualResetEvent(false);
                FileUploader uploader = new FileUploader(this.syncSetting, doneEvents[taskId], this, taskId);
                ThreadPool.QueueUserWorkItem(new WaitCallback(uploader.uploadFile), filesToUpload[taskId]);
            }

            try
            {
                WaitHandle.WaitAll(doneEvents);
            }
            catch (Exception ex)
            {
                Log.Error("wait for job to complete error, "+ex.Message);
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

                this.fileExistsWriter.AutoFlush = true;
                this.fileOverwriteWriter.AutoFlush = true;
                this.fileNotOverwriteWriter.AutoFlush = true;
                this.fileUploadSuccessWriter.AutoFlush = true;
                this.fileUploadErrorWriter.AutoFlush = true;
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
            //count
            this.updateUploadLog(string.Format("正在计算{0}下文件总数...", localSyncDir));
            this.processDirCount(localSyncDir, localSyncDir);
            //upload
            this.updateUploadLog(string.Format("开始同步{0}下所有文件...", localSyncDir));
            this.processDirUpload(localSyncDir, localSyncDir);
            if (!this.cancelSignal)
            {
                //finish the remained
                this.uploadFiles(this.batchOpFiles);
                this.batchOpFiles.Clear();
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

        internal void updateSingleFileProgress(int taskId, string fileFullPath, string fileKey, string uploadProgress)
        {
            UploadInfo uploadInfo = this.uploadInfos[taskId];
            uploadInfo.LocalPath = fileFullPath;
            uploadInfo.FileKey = fileKey;
            uploadInfo.Progress = uploadProgress;
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

        internal void addFileExistsLog(string log)
        {
            lock (this.fileExistsLock)
            {
                this.fileExistsCount += 1;
            }
            try
            {
                this.fileExistsWriter.WriteLine(log);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("write file exists log for {0} failed due to {1}", this.jobId, ex.Message));
            }
        }

        internal void addFileOverwriteLog(string log)
        {
            lock (this.fileOverwriteLock)
            {
                this.fileOverwriteCount += 1;
            }
            try
            {
                this.fileOverwriteWriter.WriteLine(log);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("write file overwrite log for {0} failed due to {1}", this.jobId, ex.Message));
            }
        }

        internal void addFileNotOverwriteLog(string log)
        {
            lock (this.fileNotOverwriteLock)
            {
                this.fileNotOverwriteCount += 1;
            }
            try
            {
                this.fileNotOverwriteWriter.WriteLine(log);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("write file not overwrite log for {0} failed due to {1}", this.jobId, ex.Message));
            }
        }

        internal void addFileUploadErrorLog(string log)
        {
            lock (this.fileUploadErrorLock)
            {
                this.fileUploadErrorCount += 1;
            }
            try
            {
                this.fileUploadErrorWriter.WriteLine(log);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("write file upload failed log for {0} failed due to {1}", this.jobId, ex.Message));
            }
        }


        internal void addFileUploadSuccessLog(string log)
        {
            lock (this.fileUploadSuccessLock)
            {
                this.fileUploadSuccessCount += 1;
            }
            try
            {
                this.fileUploadSuccessWriter.WriteLine(log);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("write file upload success log for {0} failed due to {1}", this.jobId, ex.Message));
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

       

    }
}