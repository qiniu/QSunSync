using Newtonsoft.Json;
using Qiniu.Storage;
using Qiniu.Util;
using SunSync.Models;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for AccountSettingPage.xaml
    /// </summary>
    public partial class DomainsSettingPage : Page
    {
        private MainWindow mainWindow;
        public DomainsSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.loadDomainsInfo();
            this.SettingsErrorTextBlock.Text = "";
        }

        private void BackToHome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }

        
        /// <summary>
        /// load ak & sk from local file
        /// </summary>
        private void loadDomainsInfo()
        {
            Domains domains = Domains.TryLoadDomains();
            if (!string.IsNullOrEmpty(domains.UpDomain))
            {
                this.UpDomainTextBox.Text = domains.UpDomain;
            }
            if (!string.IsNullOrEmpty(domains.RsDomain))
            {
                this.RsDomainTextBox.Text = domains.RsDomain;
            }
        }

        /// <summary>
        /// save account settings to local file and check the validity of the settings
        /// </summary>
        private void SaveDomainsSetting(object domainsObj)
        {
            Domains account = (Domains)domainsObj;
            //write settings to local file
            string domainsData = JsonConvert.SerializeObject(account);
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appDir = System.IO.Path.Combine(myDocPath, "qsunsync");
            try
            {
                if (!Directory.Exists(appDir))
                {
                    try
                    {
                        Directory.CreateDirectory(appDir);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(string.Format("create app dir {0} failed due to {1}", appDir, ex.Message));
                        Dispatcher.Invoke(new Action(delegate
                        {
                            this.SettingsErrorTextBlock.Text = "创建本地配置路径失败";
                        }));
                    }
                }
                string domainsPath = System.IO.Path.Combine(appDir, "domains.json");
                using (StreamWriter sw = new StreamWriter(domainsPath, false, Encoding.UTF8))
                {
                    sw.Write(domainsData);
                }
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "设置成功!";
                    Thread.Sleep(2000);
                    this.mainWindow.GotoHomePage();
                }));
            }
            catch (Exception ex)
            {
                Log.Error("save domains info to file failed, " + ex.Message);
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "域名设置写入文件失败";
                }));
            }

            
        }

        private void SaveDomainsSettings_EventHandler(object sender, System.Windows.RoutedEventArgs e)
        {
            string upDomain = this.UpDomainTextBox.Text.Trim();
            string rsDomain = this.RsDomainTextBox.Text.Trim();
            Domains domains = new Domains();
            domains.UpDomain = upDomain;
            domains.RsDomain = rsDomain;
            new Thread(new ParameterizedThreadStart(this.SaveDomainsSetting)).Start(domains);
        }
    }
}
