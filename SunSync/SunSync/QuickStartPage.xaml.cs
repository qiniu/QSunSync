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
            SyncRecord.CreateSyncRecordDBIfNone();
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

        /// <summary>
        /// load recent sync jobs
        /// </summary>
        private void LoadSyncRecords()
        {
            List<SyncRecord> syncRecords = SyncRecord.LoadRecentSyncJobs();

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
                SyncSetting syncSetting = SyncSetting.LoadSyncSettingByJobId(syncId);
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
            Account account = Account.TryLoadAccount();
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
