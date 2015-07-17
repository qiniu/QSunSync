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
        }

        //default page
        private void MainWindow_Loaded_EventHandler(object sender, RoutedEventArgs e)
        {
            this.GotoHomePage();
        }

        //go to home page
        internal void GotoHomePage()
        {
            this.GotoQuickStartPage();
        }

        //go to account setting page
        internal void GotoAccountPage()
        {
            this.MainHostFrame.Content = this.accountSettingPage;
        }

        //go to quick start page
        internal void GotoQuickStartPage()
        {
            this.MainHostFrame.Content = this.quickStartPage;
        }

        //go to sync setting page
        internal void GotoSyncSettingPage(SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncSettingPage;
            this.syncSettingPage.LoadSyncSetting(syncSetting);
        }

        //go to sync progress page
        internal void GotoSyncProgress(Models.SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncProgressPage;
            this.syncProgressPage.LoadSyncSetting(syncSetting);
        }

        //go to sync result page
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