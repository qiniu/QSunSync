using SunSync.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SunSync
{
    /// <summary>
    /// Interaction logic for SyncResultPage.xaml
    /// </summary>
    public partial class SyncResultPage : Page
    {
        private string jobId;
        private bool fileOverwrite;
        private int fileExistsCount;
        private int fileOverwriteCount;
        private int fileNotOverwriteCount;
        private int fileUploadErrorCount;
        private int fileUploadSuccessCount;

        private string fileExistsLogPath;
        private string fileOverwriteLogPath;
        private string fileNotOverwriteLogPath;
        private string fileUploadSuccessLogPath;
        private string fileUploadErrorLogPath;

        private Dictionary<string, string> syncResultInfo;
        private TimeSpan spentTime;
        private MainWindow mainWindow;
        public SyncResultPage(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            this.fileOverwrite = false;
            this.syncResultInfo = new Dictionary<string, string>();
            this.syncResultInfo.Add("UPLOAD_SUCCESS", "本次同步成功同步到七牛云空间中的文件数量。");
            this.syncResultInfo.Add("UPLOAD_FAILURE", "本地同步因为各种原因没有成功同步到七牛云空间中的文件数量。");
            this.syncResultInfo.Add("UPLOAD_EXISTS_MATCH", "本次同步过程中发现的已存在于云空间且本地未改动的文件数量，这些文件本地和空间内容一致，所以同步过程中自动跳过。");
            this.syncResultInfo.Add("UPLOAD_EXISTS_NO_OVERWRITE", "本次同步过程中发现的已存在于云空间且本地已有改动的文件数量，这些文件没有进行覆盖上传。如果需要覆盖上传，请在同步设置里面勾选覆盖选项。");
            this.syncResultInfo.Add("UPLOAD_EXISTS_OVERWRITE", "本次同步过程中发现的已存在于云空间且本地已有改动的文件数量，这些文件进行了覆盖上传。");
        }

        public void LoadSyncResult(string jobId,TimeSpan spentTime, bool fileOverwrite, int fileExistsCount, string fileExistsLogPath, int fileOverwriteCount,
               string fileOverwriteLogPath, int fileNotOverwriteCount, string fileNotOverwriteLogPath, int fileUploadErrorCount, string fileUploadErrorLogPath,
               int fileUploadSuccessCount, string fileUploadSuccessLogPath)
        {
            this.jobId = jobId;
            this.spentTime = spentTime;
            this.fileOverwrite = fileOverwrite;
            this.fileExistsCount = fileExistsCount;
            this.fileOverwriteCount = fileOverwriteCount;
            this.fileNotOverwriteCount = fileNotOverwriteCount;
            this.fileUploadErrorCount = fileUploadErrorCount;
            this.fileUploadSuccessCount = fileUploadSuccessCount;
            this.fileOverwriteLogPath = fileOverwriteLogPath;
            this.fileExistsLogPath = fileExistsLogPath;
            this.fileNotOverwriteLogPath = fileNotOverwriteLogPath;
            this.fileUploadErrorLogPath = fileUploadErrorLogPath;
            this.fileUploadSuccessLogPath = fileUploadSuccessLogPath;
        }

        private void SyncResultLoaded_EventHandler(object sender, RoutedEventArgs e)
        {
            Log.Info(string.Format("sync last total time {0}",this.spentTimeStr(this.spentTime.TotalSeconds)));
            //set title
            this.SyncResultTitleTextBlock.Text = this.spentTimeStr(this.spentTime.TotalSeconds);

            this.UploadSuccessTextBlock1.Text = string.Format("同步成功: {0}", this.fileUploadSuccessCount);
            this.UploadSuccessTextBlock2.Text = syncResultInfo["UPLOAD_SUCCESS"];

            this.UploadFailureTextBlock1.Text = string.Format("同步失败: {0}", this.fileUploadErrorCount);
            this.UploadFailureTextBlock2.Text = syncResultInfo["UPLOAD_FAILURE"];

            this.UploadExistsTextBlock1.Text = string.Format("智能跳过: {0}", this.fileExistsCount);
            this.UploadExistsTextBlock2.Text = syncResultInfo["UPLOAD_EXISTS_MATCH"];

            if (this.fileOverwrite)
            {
                this.UploadOverwriteTextBlock1.Text = string.Format("强制覆盖: {0}", this.fileOverwriteCount);
                this.UploadOverwriteTextBlock2.Text = syncResultInfo["UPLOAD_EXISTS_OVERWRITE"];
            }
            else
            {
                this.UploadOverwriteTextBlock1.Text = string.Format("未覆盖: {0}", this.fileNotOverwriteCount);
                this.UploadOverwriteTextBlock2.Text = syncResultInfo["UPLOAD_EXISTS_NO_OVERWRITE"];
            }
        }

        private string spentTimeStr(double seconds)
        {
            string result = "";
            if (seconds < 60)
            {
                result = string.Format("同步结果 - 耗时 {0} 秒", seconds.ToString("F"));
            }
            else if (seconds < 60 * 60)
            {
                result = string.Format("同步结果 - 耗时 {0} 分", (seconds / 60).ToString("F"));
            }
            else
            {
                result = string.Format("同步结果 - 耗时 {0} 时", (seconds / 60 / 60).ToString("F"));
            }

            return result;
        }

        private void ExportLog_EventHandler(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.Title = "选择保存文件";
            dlg.Filter = "Log (*.log)|*.log";

            System.Windows.Forms.DialogResult dr = dlg.ShowDialog();
            if (dr.Equals(System.Windows.Forms.DialogResult.OK))
            {
                string logFilePath = dlg.FileName;
                try
                {
                    using (StreamWriter sw = new StreamWriter(logFilePath, false, Encoding.UTF8))
                    {
                        if (this.fileUploadSuccessCount > 0)
                        {
                            try
                            {
                                sw.WriteLine("同步成功文件列表");
                                using (StreamReader isr = new StreamReader(this.fileUploadSuccessLogPath, Encoding.UTF8))
                                {
                                    sw.Write(isr.ReadToEnd());
                                }
                            }
                            catch (Exception) { }
                        }

                        if (this.fileUploadErrorCount > 0)
                        {
                            try
                            {
                                sw.WriteLine("同步失败文件列表");
                                using (StreamReader isr = new StreamReader(this.fileUploadErrorLogPath, Encoding.UTF8))
                                {
                                    sw.Write(isr.ReadToEnd());
                                }
                            }
                            catch (Exception) { }
                        }

                        if (this.fileExistsCount > 0)
                        {
                            try
                            {
                                sw.WriteLine("已存在，未变化文件列表");
                                using (StreamReader isr = new StreamReader(this.fileExistsLogPath, Encoding.UTF8))
                                {
                                    sw.Write(isr.ReadToEnd());
                                }
                            }
                            catch (Exception) { }
                        }

                        if (this.fileNotOverwriteCount > 0)
                        {
                            try
                            {
                                sw.WriteLine("未覆盖文件列表");
                                using (StreamReader isr = new StreamReader(this.fileNotOverwriteLogPath, Encoding.UTF8))
                                {
                                    sw.Write(isr.ReadToEnd());
                                }
                            }
                            catch (Exception) { }
                        }

                        if (this.fileOverwriteCount > 0)
                        {
                            try
                            {
                                sw.WriteLine("覆盖同步文件列表");
                                using (StreamReader isr = new StreamReader(this.fileOverwriteLogPath, Encoding.UTF8))
                                {
                                    sw.Write(isr.ReadToEnd());
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("export log for job {0} failed due to {1}",this.jobId,ex.Message));
                }
            }
        }

        private void BackToHome_EventHandler(object sender, RoutedEventArgs e)
        {
            this.mainWindow.GotoHomePage();
        }
    }
}
