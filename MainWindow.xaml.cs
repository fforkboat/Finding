using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        // 测试用，勿删
        public class _TestRecordC
        {
            public double Total { get; set; }
            public double Zip { get; set; }
            public double Doc { get; set; }
            public double Img { get; set; }
            public List<string> ZipSearched { get; } = new List<string>();
            public List<string> DocSearched { get; } = new List<string>();
            public List<string> ImgSearched { get; } = new List<string>();
            public List<string> PathFound { get; } = new List<string>();
        }
        public _TestRecordC TR { get; } = new _TestRecordC();

        // 记录耗时
        private Stopwatch stopwatch = new Stopwatch();

        // redis配置
        private const string redisConnStr = "127.0.0.1:6379,password=,DefaultDatabase=0";

        // 当前目录
        private string curDirPath;

        // 已匹配列表
        private List<string> matchedFilenameList = new List<string>();

        //已搜索的zip文件
        private HashSet<string> listSet = new HashSet<string>();

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
                OpenDirectory(folderBrowserDialog.SelectedPath);
            }
        }
        public void OpenDirectory(string path)
        {
            FilesListView.Items.Clear();

            curDirPath = path;
            string filesKey = "files:" + curDirPath;
            string subdirsKey = "subdirs:" + curDirPath;

            string[] files;
            string[] subdirs;
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
                var fileInfo = new FileInfo(file);
                FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", file));
            }

            foreach (var dir in subdirs)
            {
                var directoryInfo = new DirectoryInfo(dir);
                FilesListView.Items.Add(new FileItemInfo(directoryInfo.Name, "directory", dir));
            }
        }
        // 双击 ListViewItem 的事件处理函数，用于打开某个文件
        private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var fileItemInfo = ((ListViewItem)sender).Content as FileItemInfo;
            Process.Start(fileItemInfo.Path);
        }
        // 单击 FindButton 的事件处理函数， 用于进行文件搜索
        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            var key = Txb_Search_Key.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            SearchInSelectedDir(curDirPath, key);
        }
        /// <summary>
        /// 根据key搜索当前目录下匹配的文件
        /// </summary>
        /// <param name="dir">当前文件夹</param>
        /// <param name="key">搜索关键字</param>
        public void SearchInSelectedDir(string dir, string key)
        {
            var swTotal = Stopwatch.StartNew();

            stopwatch.Start();
            FilesListView.Items.Clear();

            string combinedKey = GetCombinedKey(key);
            if (RedisHelper.Exists(combinedKey))
            {
                MessageBox.Show("从缓存获取成功");
                matchedFilenameList = RedisHelper.ListGet<string>(combinedKey);
                foreach(var matchedPath in matchedFilenameList)
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
            } 
            else
            {
                //提前解压缩
                var sw = Stopwatch.StartNew();
                var ziper = new Ziper();
                TR.ZipSearched.AddRange(ziper.extract(dir));
                sw.Stop();
                TR.Zip = sw.Elapsed.TotalMilliseconds * 1e6;

                TR.Doc = 0;
                TR.Img = 0;
                var files = Directory.GetFiles(curDirPath, "*.*", SearchOption.AllDirectories);
                foreach (var filename in files)
                {
                    if (IsImageFile(filename))
                    {
                        TR.ImgSearched.Add(filename);
                        Console.WriteLine(filename);
                        sw = Stopwatch.StartNew();
                        ImageContainsKey(filename, key);
                        sw.Stop();
                        TR.Img += sw.Elapsed.TotalMilliseconds * 1e6;
                    }
                    else if (IsDocFile(filename))
                    {
                        TR.DocSearched.Add(filename);
                        Console.WriteLine(filename);
                        sw = Stopwatch.StartNew();
                        DocumentContainsKey(filename, key, ziper.Table);
                        sw.Stop();
                        TR.Doc += sw.Elapsed.TotalMilliseconds * 1e6;
                    }
                }
                //清除被解压的文件夹
                ziper.ClearFile();
            }

            stopwatch.Stop();
            DispElapsedTime();

            swTotal.Stop();
            TR.Total = swTotal.Elapsed.TotalMilliseconds * 1e6;
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
            DocumentContainsKey(matchedPath, key, new Dictionary<string, string>());
        }
        /// <summary>
        /// 调用外部函数
        /// </summary>
        /// <param name="filepath">待匹配文件</param>
        /// <param name="key">待搜索键</param>
        /// <returns></returns>
        private void DocumentContainsKey(string filepath, string key, Dictionary<string,string> zipDic)
        {
            using(var process = new Process())
            {
                process.StartInfo.FileName = @"java";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.Arguments = @"-jar C:\Users\andys\source\repos\searchdocs\Csapp_ReadFile.jar";
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (int.TryParse(e.Data, out int val))
                        {
                            if(val > 0)
                            {
                                //转为.zip路径
                                var isContained = false;
                                if (zipDic.ContainsKey(filepath))
                                {
                                    filepath = zipDic[filepath];
                                    if (listSet.Contains(filepath))
                                    {
                                        isContained = true;
                                    }
                                    else
                                    {
                                        //更新list中的.zip路径
                                        listSet.Add(filepath);
                                    }
                                }

                                var fileInfo = new FileInfo(filepath);
                                // 存入redis缓存
                                RedisHelper.ListPush(GetCombinedKey(key), filepath);
                                // 存入文件MD5摘要
                                RedisHelper.Set(GetMD5Key(filepath), MD5Checker.GetFileMD5(filepath));

                                // 如果.zip目录不在list中，加入UI
                                if (!isContained)
                                {
                                    FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", filepath));
                                    }));
                                }
                            }
                            process.Kill();
                        }
                    }
                });

                process.Start();//启动程序
                process.BeginOutputReadLine();
                process.StandardInput.AutoFlush = true;
                process.StandardInput.WriteLine(filepath);
                process.StandardInput.WriteLine(key);
                process.WaitForExit();
            }
        }
        /// <summary>
        /// 调用外部函数在图片中搜索
        /// </summary>
        /// <param name="filename">待匹配文件</param>
        /// <param name="key">待搜索键</param>
        private void ImageContainsKey(string filename, string key)
        {
            var oriDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(@"C:\Users\andys\source\repos\searchimg2\searchimg2");

            using(var process = new Process())
            {
                process.StartInfo.FileName = @"searchimg2.exe";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (e.Data == "True")
                        {
                            Console.WriteLine(e.Data);
                            FileInfo fileInfo = new FileInfo(filename);
                            matchedFilenameList.Add(filename);

                            FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", filename));
                            }));
                            TR.PathFound.Add(filename);
                        }
                    }
                });

                process.Start();//启动程序
                process.BeginOutputReadLine();
                process.StandardInput.AutoFlush = true;
                process.StandardInput.WriteLine(filename);
                process.StandardInput.WriteLine(key);
                process.WaitForExit();
            }
            
            Directory.SetCurrentDirectory(oriDir);
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
                var fileInfo = new FileInfo(filename);
                FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", filename));
            }
        }
        /// <summary>
        /// 显示搜索耗时
        /// </summary>
        private void DispElapsedTime()
        {
            Lbl_Used_Time.Content = string.Format("{0}s", stopwatch.Elapsed.TotalSeconds);
        }
    }
}
