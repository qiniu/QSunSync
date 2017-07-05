using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using SunSync.Models;
using System.IO;
using System.Threading;
using Qiniu.Util;
using Qiniu.Http;
using Qiniu.RS;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for AccountSettingPage.xaml
    /// </summary>
    public partial class AccountSettingPage : Page
    {
        private MainWindow mainWindow;
        private string myAKSKLink = "https://portal.qiniu.com/user/key";
        public AccountSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.loadAccountInfo();
        }

        private void BackToHome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }

        /// <summary>
        /// view my ak & sk button click handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ViewMyAKSK_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(this.myAKSKLink);
            }
            catch (Exception ex)
            {
                Log.Error("open ak & sk link failed, " + ex.Message);
            }
        }

        /// <summary>
        /// load ak & sk from local file
        /// </summary>
        private void loadAccountInfo()
        {
            Account acct = Account.TryLoadAccount();
            if (!string.IsNullOrEmpty(acct.AccessKey))
            {
                this.AccessKeyTextBox.Text = acct.AccessKey;
            }
            if (!string.IsNullOrEmpty(acct.SecretKey))
            {
                this.SecretKeyTextBox.Text = acct.SecretKey;
            }
        }

        /// <summary>
        /// save account settings to local file and check the validity of the settings
        /// </summary>
        private void SaveAccountSetting(object accountObj)
        {
            Account account = (Account)accountObj;

            //check ak & sk validity
            bool valid= ValidateAccount(account);
            if (!valid)
            {
                return;
            }

            //write settings to local file
            string accData = JsonConvert.SerializeObject(account);
            string appDir = Tools.getAppDir();
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
                string accPath = Tools.getAccountFile();
                using (StreamWriter sw = new StreamWriter(accPath, false, Encoding.UTF8))
                {
                    sw.Write(accData);
                }

                // 设置AK&SK
                SystemConfig.ACCESS_KEY = account.AccessKey;
                SystemConfig.SECRET_KEY = account.SecretKey;
                Log.Info("write the ak&sk to local file success");

                Dispatcher.Invoke(new Action(delegate {
                    //go to the home page
                    this.mainWindow.GotoHomePage();
                }));
            }
            catch (Exception ex)
            {
                Log.Error("save account info to file failed, " + ex.Message);
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "帐号设置写入文件失败";
                }));
            }
        }

        /// <summary>
        /// 使用stat模拟操作来检查Account是否正确
        /// </summary>
        /// <returns></returns>
        private bool ValidateAccount(Account account)
        {
            Dispatcher.Invoke(new Action(delegate
            {
                this.SettingsErrorTextBlock.Text = "";
            }));

            //check ak & sk validity
            Mac mac = new Mac(account.AccessKey, account.SecretKey);
            BucketManager bucketManager = new BucketManager(mac);
            int code = bucketManager.Stat("NONE_EXIST_BUCKET", "NONE_EXIST_KEY").Code;

            if (code == (int)HttpCode.OK ||
                code == (int)HttpCode.BUCKET_NOT_EXIST ||
                code == (int)HttpCode.FILE_NOT_EXIST)
            {
                Log.Info("validate account, ak & sk is valid");
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "";
                }));
                return true;
            }
            else
            {
                Log.Error("validate account, ak & sk wrong");
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "AK 或 SK 设置不正确";
                }));
                return false;
            }
        }

        /// <summary>
        /// save account settings button click handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveAccountSettings_EventHandler(object sender, System.Windows.RoutedEventArgs e)
        {
            string accessKey = this.AccessKeyTextBox.Text.Trim();
            string secretKey = this.SecretKeyTextBox.Text.Trim();
            Account account = new Account();
            account.AccessKey = accessKey;
            account.SecretKey = secretKey;
            new Thread(new ParameterizedThreadStart(this.SaveAccountSetting)).Start(account);
        }
    }
}
