using Newtonsoft.Json;
using SunSync.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for QuickStartPage.xaml
    /// </summary>
    public partial class QuickStartPage : Page
    {
        private MainWindow mainWindow;
        private Dictionary<int, string> syncRecordDict;

        public QuickStartPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.syncRecordDict = new Dictionary<int, string>();
        }

        /// <summary>
        /// quick start page loaded event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickStartPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            this.checkJobDB();
            this.LoadSyncRecords();
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

        private void checkJobDB()
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string jobsDb = System.IO.Path.Combine(myDocPath, "qsunbox", "jobs.db");
            if (!File.Exists(jobsDb))
            {
                //db not exist, create it
                string sqlStr = new StringBuilder()
                    .Append("CREATE TABLE [sync_jobs]")
                    .Append("([sync_id] CHAR(32)  UNIQUE NOT NULL PRIMARY KEY, ")
                    .Append("[sync_local_dir] VARCHAR(255)  NOT NULL,")
                    .Append("[sync_target_bucket] VARCHAR(64)  NOT NULL,")
                    .Append("[sync_prefix] VARCHAR(255),")
                    .Append("[ignore_dir] BOOLEAN  NULL,")
                    .Append("[overwrite_file] BOOLEAN  NULL,")
                    .Append("[default_chunk_size] INTEGER  NULL,")
                    .Append("[chunk_upload_threshold] INTEGER  NULL,")
                    .Append("[sync_thread_count] INTEGER  NULL,")
                    .Append("[upload_entry_domain] VARCHAR(255)  NULL,")
                    .Append("[sync_date_time] DATE  NULL )").ToString();
                SQLiteConnection.CreateFile(jobsDb);
                string conStr = new SQLiteConnectionStringBuilder { DataSource = jobsDb }.ToString();
                using (SQLiteConnection sqlCon = new SQLiteConnection(conStr))
                {
                    sqlCon.Open();
                    using (SQLiteCommand sqlCmd = new SQLiteCommand(sqlStr, sqlCon))
                    {
                        sqlCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// load recent sync jobs
        /// </summary>
        private void LoadSyncRecords()
        {
            List<SyncRecord> syncRecords = Tools.loadRecentSyncJobs();

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

        private void listBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            int selectedIndex = this.SyncHistoryListBox.SelectedIndex;
            if (selectedIndex != -1)
            {
                string syncId = this.syncRecordDict[selectedIndex];
                SyncSetting syncSetting = Tools.loadSyncSettingByJobId(syncId);
                if (syncSetting != null)
                {
                    this.mainWindow.GotoSyncSettingPage(syncSetting);
                }
                else
                {
                    //todo
                }
            }
        }

        /// <summary>
        /// check ak & sk settings
        /// </summary>
        private void checkAccountSetting()
        {
            Account account = Tools.loadAccountInfo();
            if (string.IsNullOrEmpty(account.AccessKey) || string.IsNullOrEmpty(account.SecretKey))
            {
                this.CreateNewTask_TextBlock.Foreground = System.Windows.Media.Brushes.Gray;
                this.CreateNewTask_TextBlock.IsEnabled = false;
            }
            else
            {
                this.CreateNewTask_TextBlock.Foreground = System.Windows.Media.Brushes.MediumBlue;
                this.CreateNewTask_TextBlock.IsEnabled = true;
            }
        }
    }
}
