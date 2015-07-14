using Newtonsoft.Json;
using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Qiniu.Http;
using Qiniu.Storage;
using Qiniu.Util;
using Qiniu.Storage.Model;
using System.Collections.ObjectModel;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncProgressPage.xaml
    /// </summary>
    public partial class SyncProgressPage : Page
    {
        private SyncSetting syncSetting;
        private MainWindow mainWindow;
        private object progressLock;
        private object uploadLogLock;

        private int doneCount;
        private int totalCount;
        private UploadInfo[] uploadInfos;

        private List<string> fileExistsLog;
        private object fileExistsLock;

        private List<string> fileOverwriteLog;
        private object fileOverwriteLock;

        private List<string> fileNotOverwriteLog;
        private object fileNotOverwriteLock;

        private List<string> fileUploadErrorLog;
        private object fileUploadErrorLock;

        private List<string> fileUploadSuccessLog;
        private object fileUploadSuccessLock;

        private bool cancelSignal;
        private bool finishSignal;

        public SyncProgressPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.resetSyncProgress();
        }

        internal void LoadSyncSettingAndRun(SyncSetting syncSetting)
        {
            this.syncSetting = syncSetting;
            
            string jobName = string.Join("\t", new string[] { syncSetting.SyncLocalDir, syncSetting.SyncTargetBucket, System.DateTime.Now.ToBinary().ToString() });
            string jobFileName = Convert.ToBase64String(Encoding.UTF8.GetBytes(jobName)).Replace("+", "-").Replace("/", "_");
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDir = System.IO.Path.Combine(myDocPath, "qsunbox/jobs");
            string jobPathName = System.IO.Path.Combine(jobsDir, jobFileName);
            try
            {
                if (!Directory.Exists(jobsDir))
                {
                    Directory.CreateDirectory(jobsDir);
                }
                //todo
                //check duplicate sync job

                //write sync settings to file
                using (FileStream fs = new FileStream(jobPathName, FileMode.Create, FileAccess.Write))
                {
                    string syncSettingsJson = JsonConvert.SerializeObject(syncSetting);
                    byte[] syncSettingsData = Encoding.UTF8.GetBytes(syncSettingsJson);
                    fs.Write(syncSettingsData, 0, syncSettingsData.Length);
                }

                //run sync job
                Thread jobThread = new Thread(new ThreadStart(this.runSyncJob));
                jobThread.Start();
            }
            catch (Exception)
            {
                //todo
            }


        }

        private void SyncProgressPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            this.resetSyncProgress();
        }

        private void resetSyncProgress()
        {
            this.progressLock = new object();
            this.uploadLogLock = new object();
            this.fileExistsLog = new List<string>();
            this.fileExistsLock = new object();
            this.fileOverwriteLog = new List<string>();
            this.fileOverwriteLock = new object();
            this.fileNotOverwriteLog = new List<string>();
            this.fileNotOverwriteLock = new object();
            this.fileUploadErrorLog = new List<string>();
            this.fileUploadErrorLock = new object();
            this.fileUploadSuccessLog = new List<string>();
            this.fileUploadSuccessLock = new object();
            this.cancelSignal = false;
            this.finishSignal = false;
            this.UploadActionButton.Content = "暂停";
            this.ManualFinishButton.IsEnabled = false;
            this.UploadProgressTextBlock.Text = "";
            this.UploadProgressLogTextBlock.Text = "";
            ObservableCollection<UploadInfo> dataSource = new ObservableCollection<UploadInfo>();
            this.UploadProgressDataGrid.DataContext = dataSource;
        }

        internal void processDir(string rootDir, string targetDir, List<string> fileList)
        {
            string[] fileEntries = Directory.GetFiles(targetDir);
            foreach (string fileName in fileEntries)
            {
                fileList.Add(fileName);
            }
            string[] subDirs = Directory.GetDirectories(targetDir);
            foreach (string subDir in subDirs)
            {
                processDir(rootDir, subDir, fileList);
            }
        }

        //main job scheduler
        internal void runSyncJob()
        {
            //set before run status
            this.finishSignal = false;
            this.cancelSignal = false;

            Dispatcher.Invoke(new Action(delegate{
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
                    Console.WriteLine(ex);
                }

                //if cancel signalled
                if (this.cancelSignal)
                {
                    break;
                }
            }

            //set finish signal
            this.finishSignal = true;
            if (!this.cancelSignal)
            {
                //job finish, jump to result page
                this.mainWindow.GotoSyncResultPage(this.syncSetting.OverwriteFile, fileExistsLog, fileOverwriteLog, fileNotOverwriteLog,
                    fileUploadErrorLog, fileUploadSuccessLog);
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
                this.fileExistsLog.Add(log);
            }
        }

        internal void addFileOverwriteLog(string log)
        {
            lock (this.fileOverwriteLock)
            {
                this.fileOverwriteLog.Add(log);
            }
        }

        internal void addFileNotOverwriteLog(string log)
        {
            lock (this.fileNotOverwriteLock)
            {
                this.fileNotOverwriteLog.Add(log);
            }
        }

        internal void addFileUploadErrorLog(string log)
        {
            lock (this.fileUploadErrorLock)
            {
                this.fileUploadErrorLog.Add(log);
            }
        }


        internal void addFileUploadSuccessLog(string log)
        {
            lock (this.fileUploadSuccessLock)
            {
                this.fileUploadSuccessLog.Add(log);
            }
        }

        internal bool checkCancelSignal()
        {
            return this.cancelSignal;
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
                this.syncProgressPage.updateUploadLog("准备上传文件 "+fileFullPath);
                //set upload params
                Qiniu.Common.Config.UPLOAD_HOST = this.syncSetting.UploadEntryDomain;
                Qiniu.Common.Config.UP_HOST = this.syncSetting.UploadEntryDomain;
                Qiniu.Common.Config.PUT_THRESHOLD = this.syncSetting.ChunkUploadThreshold;
                Qiniu.Common.Config.CHUNK_SIZE = this.syncSetting.DefaultChunkSize;

                //support resume upload
                string recorderKey = this.syncSetting.SyncLocalDir + ":" + this.syncSetting.SyncTargetBucket + ":" + fileKey;
                recorderKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(recorderKey)).Replace("+", "-").Replace("/", "_");

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
                StatResult statResult = bucketManager.stat(this.syncSetting.SyncTargetBucket, fileKey);
                if (!string.IsNullOrEmpty(statResult.Hash))
                {
                    string localHash = QETag.hash(fileFullPath);
                    if (statResult.Hash.Equals(localHash))
                    {
                        //same file, no need to upload
                        this.syncProgressPage.addFileExistsLog(this.syncSetting.SyncTargetBucket + "\t" +
                            fileFullPath + "\t" + fileKey);
                        this.syncProgressPage.updateUploadLog("空间已存在，跳过文件 "+fileFullPath);
                        doneEvent.Set();
                        return;
                    }
                    else
                    {
                        if (this.syncSetting.OverwriteFile)
                        {
                            overwriteKey = true;
                            this.syncProgressPage.updateUploadLog("空间已存在，将覆盖 " + fileFullPath);
                            this.syncProgressPage.addFileOverwriteLog(this.syncSetting.SyncTargetBucket + "\t" +
                                fileFullPath + "\t" + fileKey);
                        }
                        else
                        {
                            this.syncProgressPage.updateUploadLog("空间已存在，不覆盖 "+fileFullPath);
                            this.syncProgressPage.addFileNotOverwriteLog(this.syncSetting.SyncTargetBucket + "\t" +
                                fileFullPath + "\t" + fileKey);
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
                        this.syncProgressPage.updateUploadLog("上传失败 "+fileFullPath+"，"+respInfo.Error);
                        this.syncProgressPage.addFileUploadErrorLog(this.syncSetting.SyncTargetBucket + "\t" +
                                fileFullPath + "\t" + fileKey);
                    }
                    else
                    {
                        this.syncProgressPage.updateUploadLog("上传成功 "+fileFullPath);
                        this.syncProgressPage.addFileUploadSuccessLog(this.syncSetting.SyncTargetBucket + "\t" +
                                fileFullPath + "\t" + fileKey);
                    }
                    this.syncProgressPage.updateTotalUploadProgress();
                    doneEvent.Set();
                }));

            }
        }

        private void UploadActionButton_EventHandler(object sender, RoutedEventArgs e)
        {
            this.UploadActionButton.IsEnabled = false;
            if (this.cancelSignal)
            {
                //resume upload
                Thread jobThread = new Thread(new ThreadStart(this.runSyncJob));
                jobThread.Start();
                this.UploadActionButton.IsEnabled = true;
                this.UploadActionButton.Content = "暂停";
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
            this.mainWindow.GotoSyncResultPage(this.syncSetting.OverwriteFile, this.fileExistsLog, this.fileOverwriteLog, 
                this.fileNotOverwriteLog, this.fileUploadErrorLog, this.fileUploadSuccessLog);
        }
    }
}
