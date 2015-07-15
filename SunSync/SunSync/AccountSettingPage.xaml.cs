using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using SunSync.Models;
using System.IO;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for AccountSettingPage.xaml
    /// </summary>
    public partial class AccountSettingPage : Page
    {
        private MainWindow mainWindow;
        public AccountSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.loadAccountInfo();
        }

        private void ViewMyAKSK_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string myAKSKLink = "https://portal.qiniu.com/setting/key";
            System.Diagnostics.Process.Start(myAKSKLink);
        }

        private void BackToHome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }

        private void loadAccountInfo()
        {
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string accPath = System.IO.Path.Combine(myDocPath, "qsunbox", "account.json");
            if (File.Exists(accPath))
            {
                string accData = "";
                try
                {
                    using (StreamReader sr = new StreamReader(accPath, Encoding.UTF8))
                    {
                        accData = sr.ReadToEnd();
                    }
                    Account acct = JsonConvert.DeserializeObject<Account>(accData);
                    this.AccessKeyTextBox.Text = acct.AccessKey;
                    this.SecretKeyTextBox.Text = acct.SecretKey;
                }
                catch (Exception)
                {
                    //todo
                }
            }
        }

        private void AccountSettingChange_EventHandler(object sender, TextChangedEventArgs e)
        {
            string accessKey = this.AccessKeyTextBox.Text.Trim();
            string secretKey = this.SecretKeyTextBox.Text.Trim();
            Account acc = new Account();
            acc.AccessKey = accessKey;
            acc.SecretKey = secretKey;
            string accData = JsonConvert.SerializeObject(acc);
            string myDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string appDir = System.IO.Path.Combine(myDocPath, "qsunbox");
            try
            {
                if (!Directory.Exists(appDir))
                {
                    Directory.CreateDirectory(appDir);
                }
                string accPath = System.IO.Path.Combine(appDir, "account.json");
                using (StreamWriter sw = new StreamWriter(accPath, false, Encoding.UTF8))
                {
                    sw.Write(accData);
                }
            }
            catch (Exception)
            {
                //todo
            }
            SystemConfig.ACCESS_KEY = accessKey;
            SystemConfig.SECRET_KEY = secretKey;
        }
    }
}
