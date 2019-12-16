using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Finding
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        // 记录耗时
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        // redis配置
        private const string redisConnStr = "127.0.0.1:6379,password=,DefaultDatabase=0";
        // 文档搜索
        private const string DOC_EXE = @"java -jar Y:\desktop\Csapp_ReadFile_jar\Csapp_ReadFile.jar";
        private const string CMD_EXE = @"cmd.exe";
        // 当前目录
        private string curDirPath;

        // 已匹配列表
        private List<string> matchedFilenameList = new List<string>();

        // ListViewItem 绑定的数据，代表当前文件夹下的一个文件的信息
        private class FileItemInfo
        {
            // 文件名
            public string Name { set; get; }
            // 文件类型
            public string Type { set; get; }
            // 文件路径
            public string Path { set; get; }

            public FileItemInfo(string name, string type, string path)
            {
                Name = name;
                Type = type;
                Path = path;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            ConnectRedis();
            ESHelper.InitES();
        }

        /// <summary>
        /// 连接redis
        /// </summary>
        private void ConnectRedis()
        {
            try
            {
                RedisHelper.SetCon(redisConnStr);
                //MessageBox.Show("连接redis成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                throw;
            }
        }

        // 点击 OpenDirectoryMenuItem 的事件处理函数，用于打开一个文件夹
        private void OpenDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog folderBrowserDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == true)
            {
                FilesListView.Items.Clear();
                curDirPath = folderBrowserDialog.SelectedPath;

                string[] files = null;
                string[] subdirs = null;
                string filesKey = "files:" + curDirPath;
                string subdirsKey = "subdirs:" + curDirPath;

                if (RedisHelper.Exists(filesKey))
                {
                    files = (string[])RedisHelper.Get(filesKey);
                    subdirs = (string[])RedisHelper.Get(subdirsKey);
                }
                else
                {
                    files = Directory.GetFiles(curDirPath, "*.*");
                    RedisHelper.Set(filesKey, files);
                    subdirs = Directory.GetDirectories(curDirPath);
                    RedisHelper.Set(subdirsKey, subdirs);
                }

                foreach (var file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", file));
                }

                foreach (var dir in subdirs)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(dir);
                    FilesListView.Items.Add(new FileItemInfo(directoryInfo.Name, "directory", dir));
                }
            }
        }

        // 双击 ListViewItem 的事件处理函数，用于打开某个文件
        private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var fileItemInfo = ((ListViewItem)sender).Content as FileItemInfo;
            System.Diagnostics.Process.Start(fileItemInfo.Path);
        }

        // 单击 FindButton 的事件处理函数， 用于进行文件搜索
        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Txb_Search_Key.Text))
            {
                // 提示不能为空

                return;
            }

            FilesListView.Items.Clear();
            
            SearchInSelectedDir(curDirPath, Txb_Search_Key.Text);

            stopwatch.Stop();
            DispElapsedTime();
        }

        /// <summary>
        /// 根据key搜索当前目录下匹配的文件
        /// </summary>
        /// <param name="dir">当前文件夹</param>
        /// <param name="key">搜索关键字</param>
        private void SearchInSelectedDir(string dir, string key)
        {
            stopwatch.Start();

            string combinedKey = GetCombinedKey(key);

            if (RedisHelper.Exists(combinedKey))
            {
                stopwatch.Stop();
                DispElapsedTime();
                MessageBox.Show("从缓存获取成功");
                matchedFilenameList = RedisHelper.ListGet<string>(combinedKey);
                foreach(string matchedPath in matchedFilenameList)
                {
                    // 判断文件更新
                    string oriMD5 = RedisHelper.Get<string>(GetMD5Key(matchedPath));
                    string curMD5 = MD5Checker.GetFileMD5(matchedPath);
                    if(oriMD5 != curMD5)
                    {
                        // 摘要发生变化
                        // todo 移除缓存
                        RenewFile(matchedPath, key);
                    }
                }
                DispMatchedFiles();
                return;
            }

            var files = Directory.GetFiles(curDirPath, "*.*", SearchOption.AllDirectories);
            
            Ziper zip = new Ziper();
            zip.extract(dir);

            foreach (var filename in files)
            {
                if (IsImageFile(filename))
                {
                    ImageContainsKey(filename, key);
                }
                else if (IsDocFile(filename))
                {
                    DocumentContainsKey(filename, key);
                }

            }

            zip.ClearFile();
            stopwatch.Stop();
            DispElapsedTime();
        }


        private bool IsDocFile(string filename)
        {
            return filename.EndsWith(".pdf") || filename.EndsWith(".txt")
            || filename.EndsWith(".doc") || filename.EndsWith(".docx")
            || filename.EndsWith(".ppt") || filename.EndsWith(".pptx")
            || filename.EndsWith(".xls") || filename.EndsWith(".xlsx");
        }

        private bool IsImageFile(string filename)
        {
            return filename.EndsWith(".jpg") || filename.EndsWith(".jpeg")
                || filename.EndsWith(".bmp") || filename.EndsWith(".png");
        }

        /// <summary>
        /// 更新文件缓存
        /// 只针对文档文件
        /// 图片通常不考虑更新
        /// </summary>
        /// <param name="matchedPath"></param>
        private void RenewFile(string matchedPath, string key)
        {
            DocumentContainsKey(matchedPath, key);
        }


        /// <summary>
        /// 调用外部函数
        /// </summary>
        /// <param name="filepath">待匹配文件</param>
        /// <param name="key">待搜索键</param>
        /// <returns></returns>
        private void DocumentContainsKey(string filepath, string key)
        {
            Process process = new Process();
            string cmd = DOC_EXE;
            process.StartInfo.FileName = CMD_EXE;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && e.Data.Length == 1)
                {
                    if (e.Data != "0")
                    {
                        FileInfo fileInfo = new FileInfo(filepath);
                        // 存入redis缓存
                        RedisHelper.ListPush(GetCombinedKey(key), filepath);
                        // 存入文件MD5摘要
                        RedisHelper.Set(GetMD5Key(filepath), MD5Checker.GetFileMD5(filepath));
                        FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", filepath));
                        }));

                    }
                }

            });

            process.Start();//启动程序
            process.BeginOutputReadLine();
            process.StandardInput.AutoFlush = true;
            process.StandardInput.WriteLine(cmd); //向cmd窗口写入命令
            process.StandardInput.WriteLine(filepath);
            process.StandardInput.WriteLine(key);

            process.Close();
        }


        /// <summary>
        /// 调用外部函数在图片中搜索
        /// </summary>
        /// <param name="filename">待匹配文件</param>
        /// <param name="key">待搜索键</param>
        private void ImageContainsKey(string filename, string key)
        {

        }

        /// <summary>
        /// 获取当前目录与key的组合键
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetCombinedKey(string key)
        {
            return curDirPath + ":" + key;
        }
        
        
        /// <summary>
        /// 获取路径的md5的组合键
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetMD5Key(string path)
        {
            return "MD5:" + path;
        }

        /// <summary>
        /// 显示匹配文件
        /// </summary>
        private void DispMatchedFiles()
        {
            foreach (var filename in matchedFilenameList)
            {
                FileInfo fileInfo = new FileInfo(filename);
                FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", filename));
            }
        }



        /// <summary>
        /// 显示搜索耗时
        /// </summary>
        private void DispElapsedTime()
        {
            // 以秒为单位, 可修改
            Lbl_Used_Time.Content = string.Format("{0}s", stopwatch.Elapsed.TotalSeconds);

        }
    }
}
