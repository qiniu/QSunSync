using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using SunSync.Models;
using Qiniu.Common;
using Qiniu.Storage;
using Qiniu.Util;
using Qiniu.Storage.Model;
using System.Threading;
using System;
using System.Collections.ObjectModel;
using System.Data.SQLite;

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

        private Account account;
        private SyncSetting syncSetting;
        private BucketManager bucketManager;

        // [2016-09-21 18:20] 更新 by fengyh
        // ------------------ Old Version ------------------
        // 之前版本，如果开启云端检查(检查待上传的文件是否已经存在于云端)
        // 那么每次上传一个文件时都需要进行一次stat操作，这种操作对于同步效率有一定影响
        // ------------------ New Version ------------------
        // 改进之后的版本，每次上传之前先进行一次batch stat操作，避免了多次stat的时间浪费
        // 对于HASH DB的操作，改用事务模型，批量处理，加快速度

        // 是否从历史记录载入
        private bool isLoadFromRecord = false;      

        // 支持的Zone列表
        private Dictionary<ZoneID, string> zoneNames =
            new Dictionary<ZoneID, string>()
            {
                {ZoneID.CN_East, "华东"},
                {ZoneID.CN_North,"华北"},
                {ZoneID.CN_South,"华南"},
                {ZoneID.US_North,"北美"}
            };

        // 待上传文件信息
        private List<UploadItem> uploadItems = new List<UploadItem>(); 

        public SyncSettingPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.initUIGroupValues();

            this.ButtonStartSync.IsEnabled = false;
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
            this.SyncSettingTabControl.SelectedItem = this.TabItemSyncSettingsBasics;
            this.SettingsErrorTextBlock.Text = "";

            if (this.syncSetting == null)
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = "";
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;

                //advanced settings
                this.PrefixTextBox.Text = "";
                this.SkipPrefixesTextBox.Text = "";
                this.SkipSuffixesTextBox.Text = "";
                this.CheckNewFilesCheckBox.IsChecked = true;
                this.CheckBoxUseShortFilename.IsChecked = true;
                this.RadioButtonSkipDuplicate.IsChecked = true;
                this.ChunkDefaultSizeComboBox.SelectedIndex = 5; //512KB
                this.ChunkUploadThresholdSlider.Value = 100;//100MB
                this.ThreadCountSlider.Value = 10;
                this.ThreadCountLabel.Content = "10";
                this.RadioButtonDirect.IsChecked = true;
            }
            else
            {
                //basic settings
                this.SyncLocalFolderTextBox.Text = this.syncSetting.LocalDirectory;
                this.SyncTargetBucketsComboBox.SelectedIndex = -1;

                //advanced settings
                this.PrefixTextBox.Text = this.syncSetting.SyncPrefix;
                this.SkipPrefixesTextBox.Text = this.syncSetting.SkipPrefixes;
                this.SkipSuffixesTextBox.Text = this.syncSetting.SkipSuffixes;
                this.CheckNewFilesCheckBox.IsChecked = this.syncSetting.CheckNewFiles;
                this.CheckBoxUseShortFilename.IsChecked = this.syncSetting.UseShortFilename;
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
                this.ChunkUploadThresholdSlider.Value = this.syncSetting.ChunkUploadThreshold / 1024 / 1024;
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
                        this.SyncTargetBucketsComboBox.SelectedItem = this.syncSetting.TargetBucket;
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
                    this.defaultChunkSize = 512 * 1024;//512KB
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

                ZoneID zid = ZoneID.Default;

                if (isLoadFromRecord && string.Equals(targetBucket, this.syncSetting.TargetBucket))
                {
                    // 如果是从历史记录载入并且未更改Bucket，则无需查询，直接抽取
                    zid = (ZoneID)(this.syncSetting.TargetZoneID);
                }
                else
                {
                    // 否则(如果是新建任务或者更改了Bucket)，则需要向UC发送请求查询
                    zid = AutoZone.Query(this.account.AccessKey, targetBucket);
                }

                // 根据ZoneID完成相应配置
                Qiniu.Common.Config.ConfigZone(zid);

                this.TextBlockTargetZone.Text = zoneNames[zid];
            }
            catch (Exception ex)
            {
                this.SettingsErrorTextBlock.Text = ex.Message;
            }
        }

        /// <summary>
        /// 1.根据设定参数进行必要的配置
        /// 2.检查过滤之后，生成待上传的文件列表并展示
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonCheckFilesToUpload_Click(object sender, RoutedEventArgs e)
        {
            #region CHECK_SYNC_SETTINS

            // 检查并设置AK&SK
            if (string.IsNullOrEmpty(this.account.AccessKey) || string.IsNullOrEmpty(this.account.SecretKey))
            {
                this.SettingsErrorTextBlock.Text = "请设置AK&SK";
                return;
            }

            SystemConfig.ACCESS_KEY = this.account.AccessKey;
            SystemConfig.SECRET_KEY = this.account.SecretKey;

            // 检查本地同步目录与远程同步空间
            string syncDirectory = this.SyncLocalFolderTextBox.Text.Trim();
            if (string.IsNullOrEmpty(syncDirectory) || !Directory.Exists(syncDirectory))
            {
                this.SettingsErrorTextBlock.Text = "请设置本地待同步的目录";
                return;
            }
            if (this.SyncTargetBucketsComboBox.SelectedIndex < 0)
            {
                this.SettingsErrorTextBlock.Text = "请设置目标空间";
                return;
            }

            string targetBucket = this.SyncTargetBucketsComboBox.SelectedItem.ToString();

            if (this.syncSetting == null)
            {
                this.syncSetting = new SyncSetting();
            }

            this.syncSetting.LocalDirectory = syncDirectory;
            this.syncSetting.TargetBucket = targetBucket;
            this.syncSetting.SyncPrefix = this.PrefixTextBox.Text.Trim();
            this.syncSetting.SkipPrefixes = this.SkipPrefixesTextBox.Text.Trim();
            this.syncSetting.SkipSuffixes = this.SkipSuffixesTextBox.Text.Trim();
            this.syncSetting.CheckNewFiles = this.CheckNewFilesCheckBox.IsChecked.Value;
            this.syncSetting.UseShortFilename = this.CheckBoxUseShortFilename.IsChecked.Value;
            this.syncSetting.OverwriteDuplicate = this.RadioButtonOverwriteDuplicate.IsChecked.Value;
            this.syncSetting.SyncThreadCount = (int)this.ThreadCountSlider.Value;
            this.syncSetting.ChunkUploadThreshold = (int)this.ChunkUploadThresholdSlider.Value * 1024 * 1024;
            this.syncSetting.DefaultChunkSize = this.defaultChunkSize;
            this.syncSetting.UploadFromCDN = this.RadioButtonFromCDN.IsChecked.Value;

            #endregion CHECK_SYNC_SETTINS

            #region SIMULATION

            StatResult statResult = this.bucketManager.stat(this.syncSetting.TargetBucket, "NONE_EXIST_KEY");

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
                    this.SettingsErrorTextBlock.Text = "未知错误(状态代码400)";
                }
                else
                {
                    if (statResult.ResponseInfo.Error.Equals("incorrect zone"))
                    {
                        this.SettingsErrorTextBlock.Text = "上传入口机房设置错误";
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

            #endregion SIMULATION

            int numFiles = 0; // 总文件数
            int numUpload = 0;  // 待上传文件数
            uploadItems.Clear();
            this.FilesToUploadDataGrid.DataContext = null;

            List<string> localFiles = new List<string>(); // 本地待上传的文件
            List<string> saveKeys = new List<string>();   // 保存到空间文件名
            List<string> fileEtags = new List<string>();   // 待上传文件的ETAG
            List<long> lastModified = new List<long>();  // 文件最后修改时间
            List<bool> fileSkip = new List<bool>();      // 是否跳过该文件(不上传)

            long T0 = (TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0))).Ticks;

            #region TRAVERSE_LOCAL_DIRECTORY

            DirectoryInfo di = new DirectoryInfo(syncDirectory);
            FileInfo[] ffi = di.GetFiles("*.*", SearchOption.AllDirectories);
            numFiles = ffi.Length;

            string savePrefix = this.PrefixTextBox.Text.Trim();

            if (this.syncSetting.UseShortFilename)
            {
                foreach (var fi in ffi)
                {
                    localFiles.Add(fi.FullName);
                    saveKeys.Add(savePrefix + fi.Name);
                    fileEtags.Add("_");
                    lastModified.Add((fi.LastWriteTime.Ticks - T0) / 10000);
                    fileSkip.Add(false);
                }
            }
            else
            {
                foreach (var fi in ffi)
                {
                    localFiles.Add(fi.FullName);
                    saveKeys.Add(savePrefix + fi.FullName);
                    fileEtags.Add("_");
                    lastModified.Add((fi.LastWriteTime.Ticks - T0) / 10000);
                    fileSkip.Add(false);
                }
            }

            #endregion TRAVERSE_LOCAL_DIRECTORY

            #region CHECK_PREFIX_SUFFX

            string skipPrefixes = this.SkipPrefixesTextBox.Text.Trim();
            string skipSuffixes = this.SkipSuffixesTextBox.Text.Trim();

            for (int i = 0; i < numFiles; ++i)
            {
                string saveKey = saveKeys[i];

                string[] ssPrfx = skipPrefixes.Split(',');
                foreach (string prefix in ssPrfx)
                {
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        if (saveKey.StartsWith(prefix.Trim()))
                        {
                            fileSkip[i] = true;
                            break;
                        }
                    }
                }

                string[] ssSufx = skipSuffixes.Split(',');
                foreach (string suffix in ssSufx)
                {
                    if (!string.IsNullOrWhiteSpace(suffix))
                    {
                        if (saveKey.EndsWith(suffix.Trim()))
                        {
                            fileSkip[i] = true;
                            break;
                        }
                    }
                }
            }
            #endregion CHECK_PREFIX_SUFFX

            string hashDBFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "qsunsync", "local_hash.db");
            if (!File.Exists(hashDBFile))
            {
                CachedHash.CreateCachedHashDB(hashDBFile);
            }
            SQLiteConnection localHashDBConn = new SQLiteConnection(string.Format("data source = {0}", hashDBFile));
            localHashDBConn.Open();
            Dictionary<string, HashDBItem> localHashDict = CachedHash.GetAllItems(localHashDBConn);
            localHashDBConn.Close();

            #region CHECK_LOCAL_DUPLICATE

            if (this.syncSetting.CheckNewFiles)
            {
                for (int i = 0; i < numFiles; ++i)
                {
                    if (fileSkip[i]) continue;

                    string fileName = localFiles[i];

                    if (localHashDict.ContainsKey(fileName))
                    {
                        string oldEtag = localHashDict[fileName].FileHash;
                        string newEtag = QETag.hash(fileName);

                        if (string.Equals(oldEtag, newEtag))
                        {
                            fileSkip[i] = true;
                        }
                        else
                        {
                            fileEtags[i] = newEtag;
                        }
                    }
                    else
                    {
                        fileEtags[i] = QETag.hash(fileName);
                    }
                }
            }
            else
            {
                for (int i = 0; i < numFiles; ++i)
                {
                    if (fileSkip[i]) continue;
                    fileEtags[i] = QETag.hash(localFiles[i]);
                }
            }

            #endregion CHECK_LOCAL_DUPLICATE

            #region CHECK_REMOTE_DUPLICATES

            // 如果选择了“强制覆盖”，那么无需进行远程检查
            if (!this.syncSetting.OverwriteDuplicate)
            {
                List<string> remoteHash = new List<string>();
                List<long> remoteUpdate = new List<long>();

                try
                {
                    Mac mac = new Mac(this.account.AccessKey, this.account.SecretKey);
                    BucketFileHash.BatchStat(mac, targetBucket, saveKeys, fileSkip, ref remoteHash, ref remoteUpdate);

                    for (int i = 0, k = 0; i < numFiles; ++i)
                    {
                        if (fileSkip[i]) continue;

                        if (string.Equals(fileEtags[i], remoteHash[k]))
                        {
                            // 云端已存在相同文件，跳过
                            fileSkip[i] = true;
                        }

                        ++k;
                    }
                }
                catch (Exception ex)
                {
                    this.SettingsErrorTextBlock.Text = ex.Message;
                    Log.Error(ex.Message);
                }
            }

            #endregion CHECK_REMOTE_DUPLICATES

            #region SHOW_UPLOAD_DETAILS

            this.SyncSettingTabControl.SelectedItem = this.TabItemFilesToUploadDetail;

            numUpload = numFiles;

            foreach (var b in fileSkip)
            {
                if (b) --numUpload;
            }

            if (numUpload < 1)
            {
                TextBlockFilesToUploadSummery.Text = "没有待上传的文件";
                return;
            }


            double N = 0;

            for (int i = 0; i < numFiles; ++i)
            {
                if (fileSkip[i]) continue;

                string fsize = "0";

                long n = ffi[i].Length;
                double K = 1.0 * n / 1024.0;
                double M = 0.0;
                if (K > 1024.0)
                {
                    M = K / 1024.0;
                    fsize = string.Format("{0:0.00}MB", M);
                }
                else if (K > 1.0)
                {
                    fsize = string.Format("{0:0.00}KB", K);
                }
                else
                {
                    fsize = string.Format("{0}B", n);
                }

                N += n;

                uploadItems.Add(new UploadItem() 
                { 
                    LocalFile = localFiles[i], 
                    SaveKey = saveKeys[i], 
                    FileSize = fsize, 
                    FileHash = fileEtags[i], 
                    LastUpdate = lastModified[i].ToString() 
                });
            }

            string vol = "";
            double mega = 1024.0 * 1024;
            double kilo = 1024.0;
            if (N > mega)
            {
                vol = string.Format("{0:0.00}MB", N / mega);
            }
            else if (N > kilo)
            {
                vol = string.Format("{0:0.00}KB", N / kilo);
            }
            else
            {
                vol = string.Format("{0}B", N);
            }

            TextBlockFilesToUploadSummery.Text = string.Format("待上传的文件总数:{0}, 总大小:{1}", numUpload, vol);

            Dispatcher.Invoke(new Action(delegate
            {
                ObservableCollection<UploadItem> dataSource = new ObservableCollection<UploadItem>();
                foreach (var d in uploadItems)
                {
                    dataSource.Add(d);
                }
                this.FilesToUploadDataGrid.DataContext = dataSource;
            }));

            ButtonStartSync.IsEnabled = true;

            #endregion SHOW_UPLOAD_DETAILS

        }

        
        private void StartSyncButton_EventHandler(object sender, RoutedEventArgs e)
        {         
            this.ButtonStartSync.IsEnabled = false;
            this.mainWindow.SetUploadIteams(uploadItems);
            this.mainWindow.GotoSyncProgress(syncSetting);
        }


    }
}
