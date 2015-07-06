using System;
using System.Collections.Generic;
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
                byte[] accData = null;
                try
                {
                    using (FileStream fs = new FileStream(accPath, FileMode.Open, FileAccess.Read))
                    {
                        accData = new byte[fs.Length];
                        fs.Read(accData, 0, (int)fs.Length);
                    }
                    Account acct = JsonConvert.DeserializeObject<Account>(Encoding.UTF8.GetString(accData));
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
                using (FileStream fs = new FileStream(accPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] accBytes = Encoding.UTF8.GetBytes(accData);
                    fs.Write(accBytes, 0, accBytes.Length);
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
