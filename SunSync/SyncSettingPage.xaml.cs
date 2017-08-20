using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using SunSync.Models;
using Qiniu.Util;
using System.Threading;
using System;
using Qiniu.Storage;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncSettingPage.xaml
    /// </summary>
    public partial class SyncSettingPage : Page
    {
        //default chunk size
        private int defaultChunkSizeIndex;
        //upload entry domain
        private int uploadEntryDomain;
        private MainWindow mainWindow;

        private Account account;
        private SyncSetting syncSetting;
        private BucketManager bucketManager;

        public SyncSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
        }

        /// <summary>
        /// load sync settings, this method is called before the page loaded method
        /// </summary>
        /// <param name="syncSetting"></param>
        public void LoadSyncSetting(SyncSetting syncSetting)
        {
            this.syncSetting = syncSetting;
            this.bucketManager = null;
        }

        /// <summary>
        /// sync setting page loaded event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncSettingPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            this.initBucketManager();
            if (this.bucketManager != null)
            {
                //clear old buckets
                this.SyncTargetBucketsComboBox.ItemsSource = null;
                new Thread(new ThreadStart(this.reloadBuckets)).Start();
                Thread.Sleep(10);
            }
            this.initUIDefaults();   
        }

        /// <summary>
        /// init the bucket manager
        /// </summary>
        private void initBucketManager()
        {
            this.account = Account.TryLoadAccount();

            if (string.IsNullOrEmpty(account.AccessKey) || string.IsNullOrEmpty(account.SecretKey))
            {
                Log.Info("account info not set");
                this.SettingsErrorTextBlock.Text = "请返回设置 AK 和 SK";
                return;
            }
            Mac mac = new Mac(this.account.AccessKey, this.account.SecretKey);
            this.bucketManager = new BucketManager(mac, new Config());
        }

        /// <summary>
        /// init the ui values according to the syncSetting parameter
        /// </summary>
        private void initUIDefaults()
        {
            //ui settings
            this.SyncSettingTabControl.SelectedIndex = 0;
            this.SettingsErrorTextBlock.Text = "";
            if (this.syncSetting == null)
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = "";
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;
                this.FileTypeComboBox.SelectedIndex = 0;
                this.CheckRemoteDuplicateCheckBox.IsChecked = false;
                //advanced settings
                this.PrefixTextBox.Text = "";
                this.CheckNewFilesCheckBox.IsChecked = false;
                this.OverwriteFileCheckBox.IsChecked = false;
                this.IgnoreDirCheckBox.IsChecked = false;
                this.SkipPrefixesTextBox.Text = "";
                this.SkipSuffixesTextBox.Text = "";
                this.ChunkDefaultSizeComboBox.SelectedIndex = 4; //2MB
                this.ChunkUploadThresholdSlider.Value = 4;//4MB
                this.ThreadCountSlider.Value = 10;
                this.ThreadCountLabel.Content = "10";
                this.UploadByCdnRadioButton.IsChecked = true;
            }
            else
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = syncSetting.SyncLocalDir;
                this.FileTypeComboBox.SelectedIndex = syncSetting.FileType;
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;
                this.CheckRemoteDuplicateCheckBox.IsChecked = syncSetting.CheckRemoteDuplicate;
                //advanced settings
                this.PrefixTextBox.Text = syncSetting.SyncPrefix;
                this.OverwriteFileCheckBox.IsChecked = syncSetting.OverwriteFile;
                this.CheckNewFilesCheckBox.IsChecked = syncSetting.CheckNewFiles;
                this.IgnoreDirCheckBox.IsChecked = syncSetting.IgnoreDir;
                this.SkipPrefixesTextBox.Text = syncSetting.SkipPrefixes;
                this.SkipSuffixesTextBox.Text = syncSetting.SkipSuffixes;
                this.ThreadCountSlider.Value = syncSetting.SyncThreadCount;
                this.ThreadCountLabel.Content = syncSetting.SyncThreadCount.ToString();
                this.ChunkUploadThresholdSlider.Value = syncSetting.ChunkUploadThreshold / 1024 / 1024;
                this.ChunkDefaultSizeComboBox.SelectedIndex = syncSetting.DefaultChunkSize;
                switch (syncSetting.UploadEntryDomain) {
                    case 0:
                        this.UploadByCdnRadioButton.IsChecked = true;
                        break;
                    case 1:
                        this.UploadBySrcRadioButton.IsChecked = true;
                        break;
                    default:
                        this.UploadByCdnRadioButton.IsChecked = true;
                        break;
                }
            }
        }

        //reload buckets
        private void reloadBuckets()
        {
            DateTime start = System.DateTime.Now;
            //get new bucket list
            BucketsResult bucketsResult = this.bucketManager.Buckets(true);
            if (bucketsResult.Code==200)
            {
                List<string> buckets = bucketsResult.Result;
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SyncTargetBucketsComboBox.ItemsSource = buckets;
                    if (this.syncSetting != null)
                    {
                        this.SyncTargetBucketsComboBox.SelectedItem = this.syncSetting.SyncTargetBucket;
                    }
                    Log.Info("load buckets last for "+  System.DateTime.Now.Subtract(start).TotalSeconds+" seconds");
                }));
            }
            else if (bucketsResult.Code == 401)
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "AK 或 SK 不正确";
                }));
            }
            else
            {
                Log.Error(string.Format("get buckets unknown error, {0}:{1}:{2}:{3}", bucketsResult.Code,
                        bucketsResult.Text, bucketsResult.RefInfo["X-Reqid"], 
                        System.Text.Encoding.UTF8.GetString(bucketsResult.Data)));
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "未知错误，请联系七牛";
                }));
            }
        }

        /// <summary>
        /// reload buckets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReloadBucketButton_EventHandler(object sender, RoutedEventArgs e)
        {
            this.SettingsErrorTextBlock.Text = "";
            this.SyncTargetBucketsComboBox.ItemsSource = null;
            new Thread(new ThreadStart(this.reloadBuckets)).Start();
        }

        /// <summary>
        /// back to home event handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackToHome_EventHandler(object sender, MouseButtonEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }

        /// <summary>
        /// browse the local folder to sync
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrowseFolderButton_EventHandler(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.ShowNewFolderButton = false;
            DialogResult dr = fbd.ShowDialog();
            if (dr.Equals(DialogResult.OK))
            {
                this.SyncLocalFolderTextBox.Text = fbd.SelectedPath;
            }
        }


        private void StartSyncButton_EventHandler(object sender, RoutedEventArgs e)
        {
            this.SyncSettingTabControl.SelectedIndex = 0;
            //check ak & sk
            if (string.IsNullOrEmpty(this.account.AccessKey)
                || string.IsNullOrEmpty(this.account.SecretKey))
            {
                this.SettingsErrorTextBlock.Text = "请返回设置 AK & SK";
                return;
            }

            //save config to job record
            if (this.SyncLocalFolderTextBox.Text.Trim().Length == 0)
            {
                this.SettingsErrorTextBlock.Text = "请选择本地待同步目录";
                return;
            }

            if (this.SyncTargetBucketsComboBox.SelectedIndex == -1)
            {
                this.SettingsErrorTextBlock.Text = "请选择同步的目标空间";
                return;
            }

            string syncLocalDir = this.SyncLocalFolderTextBox.Text.Trim();
            if (!Directory.Exists(syncLocalDir))
            {
                //directory not found
                this.SyncSettingTabControl.SelectedIndex = 0;
                this.SettingsErrorTextBlock.Text = "本地待同步目录不存在";
                return;
            }

            string syncTargetBucket = this.SyncTargetBucketsComboBox.SelectedItem.ToString();
            StatResult statResult = this.bucketManager.Stat(syncTargetBucket, "NONE_EXIST_KEY");
    
            if (statResult.Code == 401)
            {
                //ak & sk not right
                this.SettingsErrorTextBlock.Text = "AK 或 SK 不正确";
                return;
            }
            else if (statResult.Code == 631)
            {
                //bucket not exist
                this.SettingsErrorTextBlock.Text = "指定空间不存在";
                return;
            }
            else if (statResult.Code == 612
                || statResult.Code == 200)
            {
                //file exists or not
                //ignore
            }
            else
            {
                this.SettingsErrorTextBlock.Text = "网络故障";
                Log.Error(string.Format("get buckets unknown error, {0}:{1}:{2}:{3}", statResult.Code,
                       statResult.Text, statResult.RefInfo["X-Reqid"],System.Text.Encoding.UTF8.GetString(statResult.Data)));
                return;
            }

            //set progress ak & sk
            SystemConfig.ACCESS_KEY = this.account.AccessKey;
            SystemConfig.SECRET_KEY = this.account.SecretKey;

            //optional settings
            SyncSetting syncSetting = new SyncSetting();
            syncSetting.SyncLocalDir = syncLocalDir;
            syncSetting.SyncTargetBucket = syncTargetBucket;
            syncSetting.CheckRemoteDuplicate = this.CheckRemoteDuplicateCheckBox.IsChecked.Value;
            syncSetting.SyncPrefix = this.PrefixTextBox.Text.Trim();
            syncSetting.CheckNewFiles = this.CheckNewFilesCheckBox.IsChecked.Value;
            syncSetting.IgnoreDir = this.IgnoreDirCheckBox.IsChecked.Value;
            syncSetting.SkipPrefixes = this.SkipPrefixesTextBox.Text.Trim();
            syncSetting.SkipSuffixes = this.SkipSuffixesTextBox.Text.Trim();
            syncSetting.OverwriteFile = this.OverwriteFileCheckBox.IsChecked.Value;
            syncSetting.SyncThreadCount = (int)this.ThreadCountSlider.Value;
            syncSetting.ChunkUploadThreshold = (int)this.ChunkUploadThresholdSlider.Value * 1024 * 1024;
            syncSetting.DefaultChunkSize = this.defaultChunkSizeIndex;
            syncSetting.UploadEntryDomain = this.uploadEntryDomain;

            this.mainWindow.GotoSyncProgress(syncSetting);
        }

        private void ChunkUploadThresholdChange_EventHandler(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.ChunkUploadThresholdLabel != null)
            {
                this.ChunkUploadThresholdLabel.Content = this.ChunkUploadThresholdSlider.Value + "MB";
            }
        }

        private void ThreadCountChange_EventHandler(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.ThreadCountLabel != null)
            {
                this.ThreadCountLabel.Content = this.ThreadCountSlider.Value + "";
            }
        }

        private void ChunkDefaultSizeSelectChanged_EventHandler(object sender, SelectionChangedEventArgs e)
        {
            switch (this.ChunkDefaultSizeComboBox.SelectedIndex)
            {
                case 0:
                    this.defaultChunkSizeIndex = 0; //128KB
                    break;
                case 1:
                    this.defaultChunkSizeIndex = 1; //256KB
                    break;
                case 2:
                    this.defaultChunkSizeIndex = 2; //512KB
                    break;
                case 3:
                    this.defaultChunkSizeIndex = 3; // 1024KB
                    break;
                case 4:
                    this.defaultChunkSizeIndex = 4; //2048KB
                    break;
                case 5:
                    this.defaultChunkSizeIndex = 5; //4096KB
                    break;
                default:
                    this.defaultChunkSizeIndex = 5;
                    break;
            }
        }

        private void UploadByCdnRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            this.uploadEntryDomain = 0;
        }

        private void UploadBySrcRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            this.uploadEntryDomain = 1;
        }

        private void CheckRemoteDuplicateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("您选中这个选项是因为空间可能存在同名文件吗？如果您想覆盖这些同名文件，请在【高级设置】里面选中【覆盖空间中已有同名文件】的选项，否则默认情况下，不会帮您覆盖的！",
                "提示",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }

        private void FileTypeCommon_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void FileTypeLowFrequency_Checked(object sender, RoutedEventArgs e)
        {

        }
    }

}
