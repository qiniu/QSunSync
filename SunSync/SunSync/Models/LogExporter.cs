using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SunSync.Models
{
    class LogExporter
    {
        public static void exportLog(string fileUploadSuccessLogPath,
                    string fileUploadErrorLogPath,
                    string fileSkippedLogPath,
                    string fileExistsLogPath,
                    string fileNotOverwriteLogPath,
                    string fileOverwriteLogPath, string logSaveFilePath)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(logSaveFilePath, false, Encoding.UTF8))
                {
                    string line = null;

                    FileInfo fi = new FileInfo(fileUploadSuccessLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("本次同步成功文件列表：");
                            using (StreamReader isr = new StreamReader(fileUploadSuccessLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export success file list error {0}", ex.Message));
                        }
                    }

                    fi = new FileInfo(fileUploadErrorLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("本次同步失败文件列表：");
                            using (StreamReader isr = new StreamReader(fileUploadErrorLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export failed file list error {0}", ex.Message));
                        }
                    }

                    fi = new FileInfo(fileSkippedLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("按照前缀或后缀规则跳过不进行同步的文件列表：");
                            using (StreamReader isr = new StreamReader(fileSkippedLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export skipped file list error {0}", ex.Message));
                        }
                    }

                    fi = new FileInfo(fileExistsLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("远程已存在，且本地未发生变化的文件列表：");
                            using (StreamReader isr = new StreamReader(fileExistsLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export exists file list error {0}", ex.Message));
                        }
                    }

                    fi = new FileInfo(fileNotOverwriteLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("本地文件发生改动，但远程未覆盖的文件列表：");
                            using (StreamReader isr = new StreamReader(fileNotOverwriteLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export not overwrite file list error {0}", ex.Message));
                        }
                    }

                    fi = new FileInfo(fileOverwriteLogPath);
                    if (fi.Length > 0)
                    {
                        try
                        {
                            sw.WriteLine("本地文件发生改动，远程强制覆盖的文件列表：");
                            using (StreamReader isr = new StreamReader(fileOverwriteLogPath, Encoding.UTF8))
                            {
                                while ((line = isr.ReadLine()) != null)
                                {
                                    sw.WriteLine(line);
                                }
                            }
                            sw.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("export overwrite file list error {0}", ex.Message));
                        }

                    }

                    sw.Flush();
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("export log failed due to {0}", ex.Message));
                MessageBox.Show("导出日志失败，" + ex.Message, "导出日志", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
