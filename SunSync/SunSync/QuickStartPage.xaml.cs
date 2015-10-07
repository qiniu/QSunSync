using SunSync.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for QuickStartPage.xaml
    /// </summary>
    public partial class QuickStartPage : Page
    {
        private MainWindow mainWindow;
        private Dictionary<int, string> syncRecordDict;
        private string jobsDbPath;
        private List<string> topBGImages;
        private int clickCount;
        private string myAppPath;

        public QuickStartPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.syncRecordDict = new Dictionary<int, string>();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            this.myAppPath = System.IO.Path.Combine(myDocPath, "qsunsync");
            if (!Directory.Exists(myAppPath))
            {
                try
                {
                    Directory.CreateDirectory(myAppPath);
                }
                catch (Exception ex)
                {
                    Log.Fatal(string.Format("unable to create my app path {0} due to {1}", myAppPath, ex.Message));
                }
            }
            this.jobsDbPath = System.IO.Path.Combine(myDocPath, "qsunsync", "jobs.db");
            this.topBGImages = new List<string>(); 
            this.topBGImages.Add("Pictures/qiniu_logo.jpg");
            this.topBGImages.Add("Pictures/qiniu_logo.jpg");   
            this.clickCount = 0;
        }

        /// <summary>
        /// quick start page loaded event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickStartPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(this.jobsDbPath))
            {
                try
                {
                    SyncRecord.CreateSyncRecordDB(this.jobsDbPath);
                }
                catch (Exception ex)
                {
                    Log.Fatal("create sync db failed, " + ex.Message);
                }
            }
            else
            {
                this.loadSyncRecords();
            }

            this.checkAccountSetting();
        }

        /// <summary>
        /// go to account setting page
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetAccount_EventHandler(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoAccountPage();
        }

        /// <summary>
        /// go to empty sync setting page, create new sync job
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateNewSyncJob_EventHandler(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoSyncSettingPage(null);
        }

        /// <summary>
        /// load recent sync jobs
        /// </summary>
        internal void loadSyncRecords()
        {
            try
            {
                List<SyncRecord> syncRecords = SyncRecord.LoadRecentSyncJobs(this.jobsDbPath);
                this.SyncHistoryListBox.Items.Clear();
                this.syncRecordDict.Clear();
                int index = 0;
                foreach (SyncRecord record in syncRecords)
                {
                    ListBoxItem listBoxItem = new ListBoxItem();
                    Style ctlStyle = Application.Current.TryFindResource("jobListItemResource") as Style;
                    listBoxItem.DataContext = record;
                    listBoxItem.Style = ctlStyle;
                    listBoxItem.MouseDoubleClick += listBoxItem_MouseDoubleClick;
                    listBoxItem.MouseRightButtonUp += listBoxItem_MouseRightButtonUp;
                    this.syncRecordDict.Add(index, record.SyncId);
                    this.SyncHistoryListBox.Items.Add(listBoxItem);
                    index += 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error("load recent sync jobs failed, " + ex.Message);
            }
        }

        private void listBoxItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            ContextMenu ctxMenu = new ContextMenu();
            
            MenuItem deleteJobMenuItem = new MenuItem();
            deleteJobMenuItem.Header = "删除任务";
            deleteJobMenuItem.Click += deleteJobMenuItem_Click;

            MenuItem exportJobLogMenuItem = new MenuItem();
            exportJobLogMenuItem.Header = "导出日志";
            exportJobLogMenuItem.Click += exportJobLogMenuItem_Click;

            ctxMenu.Items.Add(deleteJobMenuItem);
            ctxMenu.Items.Add(exportJobLogMenuItem);

            ListBoxItem selectedItem = (ListBoxItem)sender;
            selectedItem.ContextMenu = ctxMenu;
        }

        void exportJobLogMenuItem_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = this.SyncHistoryListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                string jobId = this.syncRecordDict[selectedIndex];
                SyncSetting syncSetting = SyncSetting.LoadSyncSettingByJobId(jobId);
                if (syncSetting != null)
                {
                    System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
                    dlg.Title = "选择保存文件";
                    dlg.Filter = "Log (*.log)|*.log";

                    System.Windows.Forms.DialogResult dr = dlg.ShowDialog();
                    if (dr.Equals(System.Windows.Forms.DialogResult.OK))
                    {
                        string logSaveFilePath = dlg.FileName;
                        LogExporter.exportLog(
                            Path.Combine(this.myAppPath, "logs", jobId, "success.log"),
                            Path.Combine(this.myAppPath, "logs", jobId, "error.log"),
                            Path.Combine(this.myAppPath, "logs", jobId, "skipped.log"),
                            Path.Combine(this.myAppPath, "logs", jobId, "exists.log"),
                            Path.Combine(this.myAppPath, "logs", jobId, "not_overwrite.log"),
                            Path.Combine(this.myAppPath, "logs", jobId, "overwrite.log"),
                            logSaveFilePath);
                    }
                }
            }
        }

        void deleteJobMenuItem_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = this.SyncHistoryListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                string jobId = this.syncRecordDict[selectedIndex];
                SyncSetting syncSetting = SyncSetting.LoadSyncSettingByJobId(jobId);
                if (syncSetting != null)
                {
                    MessageBoxResult mbr = MessageBox.Show(
                        string.Format("确认删除同步任务 {0} => {1} 么？", syncSetting.SyncLocalDir, syncSetting.SyncTargetBucket), "删除任务",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (mbr.Equals(MessageBoxResult.Yes))
                    {
                        //delete job related files
                        string[] filesToDelete ={
                            Path.Combine(this.myAppPath,"logs",jobId,"error.log"),
                            Path.Combine(this.myAppPath,"logs",jobId,"exists.log"),
                            Path.Combine(this.myAppPath,"logs",jobId,"not_overwrite.log"),
                            Path.Combine(this.myAppPath,"logs",jobId,"overwrite.log"),
                            Path.Combine(this.myAppPath,"logs",jobId,"skipped.log"),
                            Path.Combine(this.myAppPath,"logs",jobId,"success.log"),
                            Path.Combine(this.myAppPath,"synclog",jobId+".log.db"),
                            Path.Combine(this.myAppPath,"dircache",jobId+".done")
                        };

                        foreach (string path in filesToDelete)
                        {
                            try
                            {
                                File.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(string.Format("delete file {0} failed due to {1}", path, ex.Message));
                            }
                        }

                        try
                        {
                            SyncRecord.DeleteSyncJobById(jobId, this.jobsDbPath);
                        }
                        catch (Exception ex) {
                            Log.Error("delete sync job by id error, "+ex.Message);
                        }

                        this.SyncHistoryListBox.Items.RemoveAt(selectedIndex);
                        this.syncRecordDict.Remove(selectedIndex);
                    }
                }
                else
                {
                    Log.Error("load sync setting by id failed, " + jobId);
                }
            }
        }


        private void listBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.SyncHistoryListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                string jobId = this.syncRecordDict[selectedIndex];
                SyncSetting syncSetting = SyncSetting.LoadSyncSettingByJobId(jobId);
                if (syncSetting != null)
                {
                    this.mainWindow.GotoSyncSettingPage(syncSetting);
                }
                else
                {
                    Log.Error("load sync setting by id failed, " + jobId);
                }
            }
        }

        /// <summary>
        /// check ak & sk settings
        /// </summary>
        internal void checkAccountSetting()
        {
            Account account = Account.TryLoadAccount();
            if (string.IsNullOrEmpty(account.AccessKey) || string.IsNullOrEmpty(account.SecretKey))
            {
                Log.Info("no account info found");
                this.CreateNewTask_TextBlock.Foreground = System.Windows.Media.Brushes.Gray;
                this.CreateNewTask_TextBlock.IsEnabled = false;
            }
            else
            {
                this.CreateNewTask_TextBlock.Foreground = System.Windows.Media.Brushes.MediumBlue;
                this.CreateNewTask_TextBlock.IsEnabled = true;
            }
        }

        private void ChangeTopBgImage_EventHandler(object sender, MouseButtonEventArgs e)
        {
            int imgCnt = this.topBGImages.Count;
            clickCount += 1;
            int index = clickCount % imgCnt;
            this.TopLogoImage.Source = new BitmapImage(new Uri(this.topBGImages[index], UriKind.Relative));
        }

        private void AboutApp_EventHandler(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/qiniu/qsunsync");
            }
            catch (Exception) { }
        }

         
    }
}
