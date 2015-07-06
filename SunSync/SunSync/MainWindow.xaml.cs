using Newtonsoft.Json;
using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        public MainWindow()
        {
            InitializeComponent();
            this.quickStartPage = new QuickStartPage(this);
            this.accountSettingPage = new AccountSettingPage(this);
            this.syncSettingPage = new SyncSettingPage(this);
            this.syncProgressPage = new SyncProgressPage(this);
            this.loadAccountInfo();
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
            this.MainHostFrame.Content = this.quickStartPage;
        }

        internal void GotoSyncSettingPage()
        {
            this.MainHostFrame.Content = this.syncSettingPage;
        }

        internal void GotoSyncProgress(Models.SyncSetting syncSetting)
        {
            this.MainHostFrame.Content = this.syncProgressPage;
            this.syncProgressPage.LoadSyncSettingAndRun(syncSetting);
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
    }
}
