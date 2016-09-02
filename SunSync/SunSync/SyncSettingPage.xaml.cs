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

        // [2016-09-01 11:40] 更新 by fengyh
        // 上传入口选择/查询模块(ZoneInfo),请参阅Models/ZoneInfo
        private ZoneInfo zoneInfo = new ZoneInfo(); // 入口查询/选择 
        private List<int> availableZoneIndexes;     // 可用上传入口
        private bool isLoadFromRecord = false;      // 是否从历史记录载入

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

            // [2016-09-01 11:40] 更新 @fengyh
            // 如果syncSetting已被设置，就说明是从历史记录载入
            // 如果syncSetting没有设置，就说明是新建任务
            this.isLoadFromRecord = (this.syncSetting != null);
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
            //if (this.account.IsAbroad)
            //{
            //    Qiniu.Common.Config.UseZoneAWS();
            //}
            //else
            //{
            //    Qiniu.Common.Config.UseZoneNB();
            //}

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

                // UploadEntryDomain: 
                // will be set in 'SyncTargetBucketsComboBox_SelectionChanged' function
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
                if (string.IsNullOrEmpty(statResult.ResponseInfo.Error))
                {
                    this.SettingsErrorTextBlock.Text = "未知错误(状态代码400)，可能是上传入口机房选择错误";
                }
                else
                {
                    if (statResult.ResponseInfo.Error.Equals("incorrect zone"))
                    {
                        this.SettingsErrorTextBlock.Text = "请选择正确的上传入口机房";
                    }
                    else
                    {
                        this.SettingsErrorTextBlock.Text = statResult.ResponseInfo.Error;
                    }
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

        /// <summary>
        /// 列表是动态获取(由SyncTargetBucketsComboBox_SelectionChanged处理)的上传入口
        /// 根据用户选择的实际上传入口进行Qiniu.Common.Config.UseZoneXXX()配置
        /// 2016-08-11 12:20 [@fengyh](http://fengyh.cn/)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UploadEntryDomainSelectChange_EventHandler(object sender, SelectionChangedEventArgs e)
        {
            if (this.UploadEntryDomainComboBox.SelectedIndex < 0 || 
                availableZoneIndexes == null || availableZoneIndexes.Count==0 )
            {
                return;
            }

            // uploadEntryDomain = 在availableZoneIndexes中被选择的那一个zoneIndex
            this.uploadEntryDomain = availableZoneIndexes[this.UploadEntryDomainComboBox.SelectedIndex];

            // 根据选择进行Zone配置
            zoneInfo.ConfigZone(this.uploadEntryDomain);
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

        /// <summary>
        /// [2016-09-01] 更新内容：
        /// 上传入口查询模块独立出来，放在Models/ZoneInfo
        /// 如果是从历史纪录载入并且未更改Bucket，则EntryDomain等设置保持不变
        /// 否则(新建任务，或者载入记录但又更改了Bucket)，则会根据用户AK&Bucket试图查找合适的上传入口
        /// 2016-09-01 11:41 [@fengyh](http://fengyh.cn/)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncTargetBucketsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(this.SyncTargetBucketsComboBox.Items.Count == 0 || this.SyncTargetBucketsComboBox.SelectedIndex<0 )
            {
                return;
            }

            try
            {
                string targetBucket = this.SyncTargetBucketsComboBox.SelectedItem.ToString();

                if ( isLoadFromRecord && string.Equals(targetBucket,this.syncSetting.SyncTargetBucket) )
                {
                    // 如果是从历史记录载入并且未更改Bucket，则无需查询，直接从列表抽取
                    availableZoneIndexes = zoneInfo.GetAvailableZoneIndexes(this.syncSetting.UploadEntryDomain);
                }
                else 
                {
                    // 否则(如果是新建任务或者更改了Bucket)，则需要向QBox发送请求查询
                    availableZoneIndexes = zoneInfo.Query(this.account.AccessKey, targetBucket);
                }

                if (availableZoneIndexes == null || availableZoneIndexes.Count == 0)
                {
                    this.SettingsErrorTextBlock.Text = "未找到合适的EntryDomian";
                    return;
                }

                // 从全部EntryDomians中提取出可用的部分
                this.UploadEntryDomainComboBox.Items.Clear();
                List<string> allEntryDesc = zoneInfo.GetAllEntryDomains();
                foreach (int index in availableZoneIndexes)
                {
                    this.UploadEntryDomainComboBox.Items.Add(allEntryDesc[index]);
                }

                // 如果是从历史纪录导入，那么index=0表示和历史纪录选择一致
                // 如果是新建任务，那么index=0就表示选择默认
                this.UploadEntryDomainComboBox.SelectedIndex = 0;
            }
            catch(Exception ex)
            {
                this.SettingsErrorTextBlock.Text = ex.Message;
            }
        }

    }
}
