using SunSync.Models;
using System;
using System.Windows;
using System.Windows.Forms;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon nIcon;
        private QuickStartPage quickStartPage;
        private AccountSettingPage accountSettingPage;
        private SyncSettingPage syncSettingPage;
        private SyncProgressPage syncProgressPage;
        private SyncResultPage syncResultPage;
        public MainWindow()
        {
            InitializeComponent();
            //set rs host
            Qiniu.Storage.Config.DefaultRsHost = "rspub.wasuqiniu.cn";
            //init log
            Log.Init();

            try
            {
                string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                this.Title =string.Format("{0}-【私有云定制版本】 v{1}@WASU", this.Title,version);
                //init tray
                this.nIcon = new NotifyIcon();
                this.nIcon.Text = "QSunSync 七牛云文件同步";
                this.nIcon.BalloonTipText = "QSunSync 七牛云文件同步";
                this.nIcon.Icon = new System.Drawing.Icon("sunsync.ico");
                this.nIcon.Visible = false;
                this.nIcon.MouseDoubleClick += NotifyIcon_MouseDoubleClick_EventHandler;
                this.nIcon.ShowBalloonTip(2000);
                MenuItem exitItem = new MenuItem("退出 QSunSync");
                exitItem.Click += ExitApp_EventHandler;
                ContextMenu ctxMenu = new ContextMenu(new MenuItem[] { exitItem });
                this.nIcon.ContextMenu = ctxMenu;
            }
            catch (Exception ex)
            {
                Log.Error("init the tray icon failed, " + ex.Message);
            }

            //init pages
            this.quickStartPage = new QuickStartPage(this);
            this.accountSettingPage = new AccountSettingPage(this);
            this.syncSettingPage = new SyncSettingPage(this);
            this.syncProgressPage = new SyncProgressPage(this);
            this.syncResultPage = new SyncResultPage(this);
        }

        private void ExitApp_EventHandler(object sender, EventArgs e)
        {
            MessageBoxResult msgResult = System.Windows.MessageBox.Show("确认退出 QSunSync？", "退出 QSunSync",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (msgResult.Equals(MessageBoxResult.Yes))
            {
                this.nIcon.Dispose();
                Log.Close();
                Environment.Exit(1);
            }
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
        internal void GotoSyncProgress(SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncProgressPage;
            this.syncProgressPage.LoadSyncSetting(syncSetting);
        }

        //go to sync result page
        internal void GotoSyncResultPage(string jobId, TimeSpan spentTime, bool fileOverwrite,
            int fileSkippedCount, string fileSkippedLogPath,
            int fileExistsCount, string fileExistsLogPath,
            int fileOverwriteCount, string fileOverwriteLogPath,
            int fileNotOverwriteCount, string fileNotOverwriteLogPath,
            int fileUploadErrorCount, string fileUploadErrorLogPath,
            int fileUploadSuccessCount, string fileUploadSuccessLogPath)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                this.MainHostFrame.Content = this.syncResultPage;
                this.syncResultPage.LoadSyncResult(jobId, spentTime, fileOverwrite,
                    fileSkippedCount, fileSkippedLogPath,
                    fileExistsCount, fileExistsLogPath,
                    fileOverwriteCount, fileOverwriteLogPath,
                    fileNotOverwriteCount, fileNotOverwriteLogPath,
                    fileUploadErrorCount, fileUploadErrorLogPath,
                    fileUploadSuccessCount, fileUploadSuccessLogPath);
            }));
        }

        private void MainWindow_Closing_EventHandler(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            this.nIcon.Visible = true;
        }

        private void NotifyIcon_MouseDoubleClick_EventHandler(object sender, MouseEventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.nIcon.Visible = false;
            this.ShowInTaskbar = true;
        }
    }
}