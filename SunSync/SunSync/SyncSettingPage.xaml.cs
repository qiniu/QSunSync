using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using SunSync.Models;
using Qiniu.Storage;
using Qiniu.Util;
using Qiniu.Storage.Model;
using System.Threading;
using System;
namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncSettingPage.xaml
    /// </summary>
    public partial class SyncSettingPage : Page
    {
        //default chunk size
        private int defaultChunkSize;
        //upload entry domain
        private int uploadEntryDomain;

        private Dictionary<int, int> defaultChunkDict;
        private MainWindow mainWindow;

        private Account account;
        private SyncSetting syncSetting;
        private BucketManager bucketManager;

        public SyncSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.initUIGroupValues();
        }

        /// <summary>
        /// init the default ui group values 
        /// </summary>
        private void initUIGroupValues()
        {
            this.defaultChunkDict = new Dictionary<int, int>();
            this.defaultChunkDict.Add(128 * 1024, 0);
            this.defaultChunkDict.Add(256 * 1024, 1);
            this.defaultChunkDict.Add(512 * 1024, 2);
            this.defaultChunkDict.Add(1 * 1024 * 1024, 3);
            this.defaultChunkDict.Add(2 * 1024 * 1024, 4);
            this.defaultChunkDict.Add(4 * 1024 * 1024, 5);
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
            this.bucketManager = new BucketManager(mac);
        }

        /// <summary>
        /// init the ui values according to the syncSetting parameter
        /// </summary>
        private void initUIDefaults()
        {
            //ui settings
            this.SyncSettingTabControl.SelectedIndex = 0;
            this.SettingsErrorTextBlock.Text = "";
            if (this.account.IsAbroad)
            {
                Qiniu.Common.Config.UseZoneAWS();
            }
            else
            {
                Qiniu.Common.Config.UseZoneNB();
            }

            if (this.syncSetting == null)
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = "";
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;
                this.CheckRemoteDuplicateCheckBox.IsChecked = false;
                //advanced settings
                this.PrefixTextBox.Text = "";
                this.CheckNewFilesCheckBox.IsChecked = false;
                this.OverwriteFileCheckBox.IsChecked = false;
                this.IgnoreDirCheckBox.IsChecked = false;
                this.SkipPrefixesTextBox.Text = "";
                this.SkipSuffixesTextBox.Text = "";
                this.ChunkDefaultSizeComboBox.SelectedIndex = 5; //512KB
                this.ChunkUploadThresholdSlider.Value = 100;//100MB
                this.ThreadCountSlider.Value = 10;
                this.ThreadCountLabel.Content = "10";
                if (this.account.IsAbroad)
                {
                    this.UploadEntryDomainComboBox.SelectedIndex = 3;//aws
                }
                else
                {
                    this.UploadEntryDomainComboBox.SelectedIndex = 0; //nb
                }
            }
            else
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = syncSetting.SyncLocalDir;
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
                int defaultChunkSizeIndex = 2;
                if (this.defaultChunkDict.ContainsKey(syncSetting.DefaultChunkSize))
                {
                    defaultChunkSizeIndex = this.defaultChunkDict[syncSetting.DefaultChunkSize];
                }
                this.ChunkDefaultSizeComboBox.SelectedIndex = defaultChunkSizeIndex;
                this.UploadEntryDomainComboBox.SelectedIndex = syncSetting.UploadEntryDomain;
            }
        }

        //reload buckets
        private void reloadBuckets()
        {
            //get new bucket list
            BucketsResult bucketsResult = this.bucketManager.buckets();
            if (bucketsResult.ResponseInfo.isOk())
            {
                List<string> buckets = bucketsResult.Buckets;
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SyncTargetBucketsComboBox.ItemsSource = buckets;
                    if (this.syncSetting != null)
                    {
                        this.SyncTargetBucketsComboBox.SelectedItem = this.syncSetting.SyncTargetBucket;
                    }
                }));
            }
            else if (bucketsResult.ResponseInfo.isNetworkBroken())
            {
                Log.Error("get buckets network error, " + bucketsResult.ResponseInfo.ToString());
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "网络故障";
                }));
            }
            else if (bucketsResult.ResponseInfo.StatusCode == 401)
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "AK 或 SK 不正确";
                }));
            }
            else if (bucketsResult.ResponseInfo.StatusCode != 0)
            {
                //status code exists,ignore
            }
            else
            {
                Log.Error(string.Format("get buckets unknown error, {0}:{1}:{2}:{3}", bucketsResult.ResponseInfo.StatusCode,
                        bucketsResult.ResponseInfo.Error, bucketsResult.ResponseInfo.ReqId, bucketsResult.Response));
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
            StatResult statResult = this.bucketManager.stat(syncTargetBucket, "NONE_EXIST_KEY");
            if (statResult.ResponseInfo.isNetworkBroken())
            {
                this.SettingsErrorTextBlock.Text = "网络故障";
                return;
            }

            if (statResult.ResponseInfo.StatusCode == 401)
            {
                //ak & sk not right
                this.SettingsErrorTextBlock.Text = "AK 或 SK 不正确";
                return;
            }
            else if (statResult.ResponseInfo.StatusCode == 631)
            {
                //bucket not exist
                this.SettingsErrorTextBlock.Text = "指定空间不存在";
                return;
            }
            else if (statResult.ResponseInfo.StatusCode == 612
                || statResult.ResponseInfo.StatusCode == 200)
            {
                //file exists or not
                //ignore
            }
            else if (statResult.ResponseInfo.StatusCode == 400)
            {
                if (statResult.ResponseInfo.Error.Equals("incorrect zone"))
                {
                    this.SettingsErrorTextBlock.Text = "请选择正确的上传入口机房";
                }
                else
                {
                    this.SettingsErrorTextBlock.Text = statResult.ResponseInfo.Error;
                }
                return;
            }
            else
            {
                this.SettingsErrorTextBlock.Text = "未知错误，请联系七牛";
                Log.Error(string.Format("get buckets unknown error, {0}:{1}:{2}:{3}", statResult.ResponseInfo.StatusCode,
                       statResult.ResponseInfo.Error, statResult.ResponseInfo.ReqId, statResult.Response));
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
            syncSetting.DefaultChunkSize = this.defaultChunkSize;
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

        private void UploadEntryDomainSelectChange_EventHandler(object sender, SelectionChangedEventArgs e)
        {
            this.uploadEntryDomain = this.UploadEntryDomainComboBox.SelectedIndex;
            switch (this.uploadEntryDomain)
            {
                case 0:
                    Qiniu.Common.Config.UseZoneNB();
                    break;
                case 1:
                    Qiniu.Common.Config.UseZoneBC();
                    break;
                case 2:
                    Qiniu.Common.Config.UseZoneAbroadNB();
                    break;
                case 3:
                    Qiniu.Common.Config.UseZoneAWS();
                    break;
            }
        }

        private void ChunkDefaultSizeSelectChanged_EventHandler(object sender, SelectionChangedEventArgs e)
        {
            switch (this.ChunkDefaultSizeComboBox.SelectedIndex)
            {
                case 0:
                    this.defaultChunkSize = 128 * 1024;//128KB
                    break;
                case 1:
                    this.defaultChunkSize = 256 * 1024;//256KB;
                    break;
                case 2:
                    this.defaultChunkSize = 512 * 1024;//512KB
                    break;
                case 3:
                    this.defaultChunkSize = 1024 * 1024;//1MB
                    break;
                case 4:
                    this.defaultChunkSize = 2 * 1024 * 1024;//2MB
                    break;
                case 5:
                    this.defaultChunkSize = 4 * 1024 * 1024; //4MB
                    break;
                default:
                    this.defaultChunkSize = 512 * 1024;//512KB
                    break;
            }
        }
    }

}
