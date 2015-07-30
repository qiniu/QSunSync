using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
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

        public QuickStartPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.syncRecordDict = new Dictionary<int, string>();
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string myAppPath = System.IO.Path.Combine(myDocPath, "qsunsync");
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
            this.topBGImages.Add("Pictures/sun_logo.jpg");   
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


        internal void listBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.SyncHistoryListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                string syncId = this.syncRecordDict[selectedIndex];
                SyncSetting syncSetting = SyncSetting.LoadSyncSettingByJobId(syncId);
                if (syncSetting != null)
                {
                    this.mainWindow.GotoSyncSettingPage(syncSetting);
                }
                else
                {
                    Log.Error("load sync setting by id failed, " + syncId);
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
    }
}
