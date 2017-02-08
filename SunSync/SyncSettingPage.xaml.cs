using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using System.Threading;
using System;
using SunSync.Models;
using Qiniu.Common;
using Qiniu.RS;
using Qiniu.RS.Model;
using Qiniu.Http;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncSettingPage.xaml
    /// </summary>
    public partial class SyncSettingPage : Page
    {
        //default chunk size
        private int defaultChunkSize;

        private Dictionary<int, int> defaultChunkDict;
        private MainWindow mainWindow;

        private SyncSetting syncSetting;
        private BucketManager bucketManager;

        // [2016-09-21 18:20] 更新 by fengyh
        // ------------------ Old Version ------------------
        // 之前版本，如果开启云端检查(检查待上传的文件是否已经存在于云端)
        // 那么每次上传一个文件时都需要进行一次stat操作，这种操作对于同步效率有一定影响
        // ------------------ New Version ------------------
        // 改进之后的版本，每次上传之前先进行一次batch stat操作，避免了多次stat的时间浪费
        // 对于HASH DB的操作，改用事务模型，批量处理，加快速度

        // 用于构建(Bucket-ZoneID)列表


        private bool isLoadedFromRecord;
        private Dictionary<string, ZoneID> zoneDict = new Dictionary<string, ZoneID>();

        // 支持的Zone列表
        private Dictionary<ZoneID, string> zoneNames =
            new Dictionary<ZoneID, string>()
            {
                {ZoneID.CN_East, "华东"},
                {ZoneID.CN_North,"华北"},
                {ZoneID.CN_South,"华南"},
                {ZoneID.US_North,"北美"}
            };

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
            if (syncSetting == null)
            {
                isLoadedFromRecord = false;
                this.syncSetting = new SyncSetting();
            }
            else
            {
                isLoadedFromRecord = true;
                this.syncSetting = syncSetting;
            }

            // 初始化bucketManager
            var mac = new Qiniu.Util.Mac(SystemConfig.ACCESS_KEY, SystemConfig.SECRET_KEY);
            this.bucketManager = new BucketManager(mac);
        }

        /// <summary>
        /// sync setting page loaded event handler
        /// 更新 2016-10-20 15:51
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncSettingPageLoaded_EventHandler(object sender, RoutedEventArgs e)
        {                
            // 重建bucket列表
            this.SyncTargetBucketsComboBox.ItemsSource = null;
            new Thread(new ThreadStart(this.reloadBuckets)).Start();
            
            // 其他界面元素初始化
            this.initUIDefaults();
        }

        /// <summary>
        /// init the ui values according to the syncSetting parameter
        /// </summary>
        private void initUIDefaults()
        {
            //ui settings
            this.SyncSettingTabControl.SelectedItem = this.TabItemSyncSettingsBasics;
            this.SettingsErrorTextBlock.Text = "";

            if (isLoadedFromRecord)
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = this.syncSetting.LocalDirectory;
                this.SyncTargetBucketsComboBox.SelectedItem = this.syncSetting.TargetBucket;

                //advanced settings
                this.PrefixTextBox.Text = this.syncSetting.SyncPrefix;
                this.SkipPrefixesTextBox.Text = this.syncSetting.SkipPrefixes;
                this.SkipSuffixesTextBox.Text = this.syncSetting.SkipSuffixes;
                this.CheckNewFilesCheckBox.IsChecked = this.syncSetting.CheckNewFiles;
                switch(this.syncSetting.FilenameKind)
                {
                    case 0:
                        this.RadioButtonUseFullFilename.IsChecked = true;
                        break;
                    case 1:
                        this.RadioButtonUseRelativePath.IsChecked = true;
                        break;
                    case 2:
                    default:
                        this.RadioButtonUseShortFilename.IsChecked = true;
                        break;
                }
                if (this.syncSetting.OverwriteDuplicate)
                {
                    this.RadioButtonOverwriteDuplicate.IsChecked = true;
                }
                else
                {
                    this.RadioButtonSkipDuplicate.IsChecked = true;
                }
                this.ThreadCountSlider.Value = this.syncSetting.SyncThreadCount;
                this.ThreadCountLabel.Content = this.syncSetting.SyncThreadCount.ToString();
                this.ChunkUploadThresholdSlider.Value = this.syncSetting.ChunkUploadThreshold / (1024 * 1024);
                int defaultChunkSizeIndex = 2;
                if (this.defaultChunkDict.ContainsKey(this.syncSetting.DefaultChunkSize))
                {
                    defaultChunkSizeIndex = this.defaultChunkDict[syncSetting.DefaultChunkSize];
                }
                this.ChunkDefaultSizeComboBox.SelectedIndex = defaultChunkSizeIndex;

                if (this.syncSetting.UploadFromCDN)
                {
                    this.RadioButtonFromCDN.IsChecked = true;
                }
                else
                {
                    this.RadioButtonDirect.IsChecked = true;
                }
            }
            else
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = "";
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;

                //advanced settings
                this.PrefixTextBox.Text = "";
                this.SkipPrefixesTextBox.Text = "";
                this.SkipSuffixesTextBox.Text = "";
                this.CheckNewFilesCheckBox.IsChecked = true;
                this.RadioButtonUseShortFilename.IsChecked = true;
                this.RadioButtonSkipDuplicate.IsChecked = true;
                this.ChunkDefaultSizeComboBox.SelectedIndex = 5; //2MB
                this.ChunkUploadThresholdSlider.Value = 4;//4MB
                this.ThreadCountSlider.Value = 10;
                this.ThreadCountLabel.Content = "10";
                this.RadioButtonFromCDN.IsChecked = true;
            }
        }

        //reload buckets
        private void reloadBuckets()
        {
            //get new bucket list
            BucketsResult bucketsResult = this.bucketManager.Buckets();
            if (bucketsResult.Code == (int)HttpCode.OK)
            {
                List<string> buckets = bucketsResult.Result;

                zoneDict.Clear();
                foreach (string bucket in buckets)
                {
                    ZoneID zoneId = ZoneHelper.QueryZone(SystemConfig.ACCESS_KEY, bucket);
                    zoneDict.Add(bucket, zoneId);
                }

                Dispatcher.Invoke(new Action(delegate
                {
                    this.SyncTargetBucketsComboBox.ItemsSource = buckets;
                }));
            }
            else
            {
                Log.Error("get buckets error, " + bucketsResult.Text);
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "获取空间列表时出错";
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
                this.SyncLocalFolderTextBox.Text = fbd.SelectedPath.Replace('\\', '/');
            }
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
                    this.defaultChunkSize = 2 * 1024 * 1024; //2MB
                    break;
            }
        }

        /// <summary>
        /// 如果是从历史纪录载入并且未更改Bucket，则EntryDomain等设置保持不变
        /// 否则(新建任务，或者载入记录但又更改了Bucket)，则会根据用户AK&Bucket试图查找合适的上传入口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SyncTargetBucketsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.SyncTargetBucketsComboBox.SelectedIndex < 0)
            {
                this.TextBlockTargetZone.Text = "";
                return;
            }

            try
            {
                string targetBucket = this.SyncTargetBucketsComboBox.SelectedItem.ToString();
                ZoneID zoneId = zoneDict[targetBucket];               
                this.TextBlockTargetZone.Text = "目标空间所在机房: " + zoneNames[zoneId];
            }
            catch (Exception ex)
            {
                this.SettingsErrorTextBlock.Text = ex.Message;
            }
        }

        /// <summary>
        /// 使用stat模拟操作来检查Account是否正确
        /// </summary>
        /// <returns></returns>
        private bool ValidateAccount()
        {
            int code = bucketManager.Stat("NONE_EXIST_BUCKET", "NONE_EXIST_KEY").Code;

            if (code == 631 || code == 612 || code == 200)
            {
                Log.Info("ak & sk is valid");
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "";
                    this.mainWindow.GotoHomePage();
                }));
            }
            else
            {
                Log.Error("ak & sk wrong");
                Dispatcher.Invoke(new Action(delegate
                {
                    this.SettingsErrorTextBlock.Text = "AK 或 SK 设置不正确";
                }));
            }

            return true;
        }

        /// <summary>
        /// 检查基本设置
        /// </summary>
        private bool CheckSyncSettings()
        {
            // 检查本地同步目录与远程同步空间
            string syncDirectory = this.SyncLocalFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(syncDirectory) || !Directory.Exists(syncDirectory))
            {
                this.SettingsErrorTextBlock.Text = "请设置本地待同步的目录";
                return false;
            }
            if (this.SyncTargetBucketsComboBox.SelectedIndex < 0)
            {
                this.SettingsErrorTextBlock.Text = "请设置目标空间";
                return false;
            }

            string targetBucket = this.SyncTargetBucketsComboBox.SelectedItem.ToString();
            ZoneID targetZoneId = zoneDict[targetBucket];

            bool fnk0 = this.RadioButtonUseFullFilename.IsChecked.Value;
            bool fnk1 = this.RadioButtonUseRelativePath.IsChecked.Value;

            // 完成syncSetting配置
            this.syncSetting.LocalDirectory = syncDirectory;
            this.syncSetting.TargetBucket = targetBucket;
            this.syncSetting.TargetZoneID = (int)targetZoneId;
            this.syncSetting.SyncThreadCount = (int)this.ThreadCountSlider.Value;
            this.syncSetting.SyncPrefix = this.PrefixTextBox.Text.Trim();
            this.syncSetting.SkipPrefixes = this.SkipPrefixesTextBox.Text.Trim();
            this.syncSetting.SkipSuffixes = this.SkipSuffixesTextBox.Text.Trim();
            this.syncSetting.CheckNewFiles = this.CheckNewFilesCheckBox.IsChecked.Value;
            this.syncSetting.FilenameKind = fnk0 ? 0 : (fnk1 ? 1 : 2);
            this.syncSetting.OverwriteDuplicate = this.RadioButtonOverwriteDuplicate.IsChecked.Value;
            this.syncSetting.DefaultChunkSize = this.defaultChunkSize;
            this.syncSetting.ChunkUploadThreshold = (int)this.ChunkUploadThresholdSlider.Value * 1024 * 1024;
            this.syncSetting.UploadFromCDN = this.RadioButtonFromCDN.IsChecked.Value;

            // 根据ZoneID完成相应配置
            Config.SetZone(targetZoneId, false);

            return true;
        }
        
        private void StartSyncButton_EventHandler(object sender, RoutedEventArgs e)
        {
            if (CheckSyncSettings())
            {
                this.mainWindow.GotoSyncProgress(syncSetting);
            }
        }

    }
}
