using Newtonsoft.Json;
using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private QuickStartPage quickStartPage;
        private AccountSettingPage accountSettingPage;
        private SyncSettingPage syncSettingPage;
        private SyncProgressPage syncProgressPage;
        private SyncResultPage syncResultPage;
        public MainWindow()
        {
            InitializeComponent();
            this.quickStartPage = new QuickStartPage(this);
            this.accountSettingPage = new AccountSettingPage(this);
            this.syncSettingPage = new SyncSettingPage(this);
            this.syncProgressPage = new SyncProgressPage(this);
            this.syncResultPage = new SyncResultPage(this);
            this.loadAccountInfo();
            this.loadRecentSyncJobs();
        }

        private void loadAccountInfo()
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string accPath = System.IO.Path.Combine(myDocPath, "qsunbox", "account.json");
            if (File.Exists(accPath))
            {
                byte[] accData = null;
                try
                {
                    using (FileStream fs = new FileStream(accPath, FileMode.Open, FileAccess.Read))
                    {
                        accData = new byte[fs.Length];
                        fs.Read(accData, 0, (int)fs.Length);
                    }
                    Account acct = JsonConvert.DeserializeObject<Account>(Encoding.UTF8.GetString(accData));
                    SystemConfig.ACCESS_KEY = acct.AccessKey;
                    SystemConfig.SECRET_KEY = acct.SecretKey;
                }
                catch (Exception)
                {
                    //todo
                }
            }
        }

        private List<SyncRecord> loadRecentSyncJobs()
        {
            List<SyncRecord> syncRecords = new List<SyncRecord>();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDir = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs");
            if (Directory.Exists(jobsDir))
            {
                string[] jobNamePaths = Directory.GetFiles(jobsDir);
                foreach (string jobNamePath in jobNamePaths)
                {
                    string jobName = System.IO.Path.GetFileName(jobNamePath);
                    try
                    {
                        string[] items = Encoding.UTF8.GetString(Convert.FromBase64String(jobName)).Split('\t');
                        if (items.Length == 3)
                        {
                            string localDir = items[0];
                            string targetBucket = items[1];
                            string binaryDate = items[2];
                            DateTime syncDate = DateTime.FromBinary(Convert.ToInt64(binaryDate));
                            SyncRecord syncRecord = new SyncRecord();
                            syncRecord.FilePath = jobNamePath;
                            syncRecord.SyncLocalDir = localDir;
                            syncRecord.SyncTargetBucket = targetBucket;
                            syncRecord.SyncDateTime = syncDate;
                            syncRecord.SyncDateTimeStr = syncDate.ToString("yyy-MM-dd HH:mm:ss");
                            syncRecords.Add(syncRecord);
                        }
                    }
                    catch (Exception) { }
                }
            }
            syncRecords.Sort(new Comparison<SyncRecord>(delegate(SyncRecord a, SyncRecord b)
            {
                return (int)(b.SyncDateTime.Subtract(a.SyncDateTime).TotalSeconds);
            }));
            return syncRecords;
        }

        private void MainWindow_Loaded_EventHandler(object sender, RoutedEventArgs e)
        {
            this.GotoHomePage();
        }

        internal void GotoAccountPage()
        {
            this.MainHostFrame.Content = this.accountSettingPage;
        }

        internal void GotoHomePage()
        {
            List<SyncRecord> syncRecords = loadRecentSyncJobs();
            this.quickStartPage.LoadSyncRecords(syncRecords);
            this.MainHostFrame.Content = this.quickStartPage;
        }

        internal void GotoSyncSettingPage(SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncSettingPage;
            this.syncSettingPage.LoadSyncSetting(syncSetting);
        }

        internal void GotoSyncProgress(Models.SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncProgressPage;
            this.syncProgressPage.LoadSyncSetting(syncSetting);
        }

        internal void GotoSyncResultPage(TimeSpan spentTime, bool fileOverwrite, int fileExistsCount, string fileExistsLogPath, int fileOverwriteCount,
               string fileOverwriteLogPath, int fileNotOverwriteCount, string fileNotOverwriteLogPath, int fileUploadErrorCount, string fileUploadErrorLogPath,
               int fileUploadSuccessCount, string fileUploadSuccessLogPath)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                this.MainHostFrame.Content = this.syncResultPage;
                this.syncResultPage.LoadSyncResult(spentTime, fileOverwrite, fileExistsCount, fileExistsLogPath, fileOverwriteCount, fileOverwriteLogPath,
                    fileNotOverwriteCount, fileNotOverwriteLogPath, fileUploadErrorCount, fileUploadErrorLogPath, fileUploadSuccessCount, fileUploadSuccessLogPath);
            }));
        }
    }
}