using libfindexor;
using MahApps.Metro.Controls;
using Nest;
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

        // ES配置
        static readonly string defaultESIndex = "findexor";
        private ElasticClient escli = null;

        // 根目录
        static readonly string rootDir = @"c:\users\andys";

        // 当前目录
        private string curDir = null;

        // 已匹配列表
        private List<string> matchedFilenameList = new List<string>();

        //已搜索的zip文件
        private HashSet<string> listSet = new HashSet<string>();

        static readonly string zipExtractSuffix = "_zip_extracted_findexor";

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
            //ConnectRedis();
            ConnectES();
            //ESHelper.InitES();
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
        private void ConnectES()
        {
            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(node);
            settings.DefaultIndex(defaultESIndex);
            escli = new ElasticClient(settings);

            if (!escli.Indices.Exists(defaultESIndex).Exists)
            {
                throw new InvalidOperationException(string.Format("index {0} not found.", defaultESIndex));
            }
        }
        // 点击 OpenDirectoryMenuItem 的事件处理函数，用于打开一个文件夹
        private void OpenDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog folderBrowserDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == true)
            {
                var starterPath = folderBrowserDialog.SelectedPath.StartsWith(rootDir) ? folderBrowserDialog.SelectedPath : rootDir;
                OpenDirectory(starterPath);
            }
        }
        public void OpenDirectory(string starterPath)
        {
            FilesListView.Dispatcher.BeginInvoke(new Action(() =>
            {
                FilesListView.Items.Clear();
            }));
            curDir = starterPath;

            //string filesKey = "files:" + starterPath;
            //string subdirsKey = "subdirs:" + starterPath;

            //string[] files;
            //string[] subdirs;
            //if (RedisHelper.Exists(filesKey))
            //{
            //    files = (string[])RedisHelper.Get(filesKey);
            //    subdirs = (string[])RedisHelper.Get(subdirsKey);
            //}
            //else
            //{
            //    files = Directory.GetFiles(curDirPath, "*.*");
            //    RedisHelper.Set(filesKey, files);
            //    subdirs = Directory.GetDirectories(curDirPath);
            //    RedisHelper.Set(subdirsKey, subdirs);
            //}

            var files = Directory.GetFiles(starterPath, "*.*");
            var subdirs = Directory.GetDirectories(starterPath);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    FilesListView.Items.Add(new FileItemInfo(fileInfo.Name, "file", file));
                }));
            }

            foreach (var dir in subdirs)
            {
                var directoryInfo = new DirectoryInfo(dir);
                FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    FilesListView.Items.Add(new FileItemInfo(directoryInfo.Name, "directory", dir));
                }));
            }
        }
        // 双击 ListViewItem 的事件处理函数，用于打开某个文件
        private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var info = ((ListViewItem)sender).Content as FileItemInfo;
            var fp = GetRealPath(info.Path);
            if (!File.Exists(fp) && !Directory.Exists(fp))
            {
                Console.WriteLine("{0} does not exist.", fp);
                return;
            }
            var argument = "/select, \"" + fp + "\"";
            Process.Start("explorer.exe", argument);
        }
        // 单击 FindButton 的事件处理函数， 用于进行文件搜索
        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            var key = Txb_Search_Key.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }
            SearchInSelectedDir(key);
        }
        private string GetRealParentDirName(string path)
        {
            var idx = path.IndexOf(zipExtractSuffix);
            if(idx != -1)
            {
                idx = path.LastIndexOf('\\', idx);
            }
            else
            {
                idx = path.LastIndexOf('\\');
            }
            return path.Substring(0, idx);
        }
        private string GetRealFileName(string path)
        {
            var slash = path.LastIndexOf('\\');
            var idx = path.IndexOf(zipExtractSuffix);
            if(idx != -1)
            {
                slash = path.LastIndexOf('\\', idx);
                var zipName = path.Substring(slash + 1, idx - slash - 1);
                return zipName + ".zip";
            }
            return path.Substring(slash + 1);
        }
        private string GetRealPath(string path)
        {
            return string.Format(@"{0}\{1}", GetRealParentDirName(path), GetRealFileName(path));
        }
        /// <summary>
        /// 根据key搜索当前目录下匹配的文件
        /// </summary>
        /// <param name="dir">当前文件夹</param>
        /// <param name="key">搜索关键字</param>
        public void SearchInSelectedDir(string key)
        {
            if(curDir == null)
            {
                curDir = rootDir;
            }

            FilesListView.Dispatcher.BeginInvoke(new Action(() =>
            {
                FilesListView.Items.Clear();
            }));
            var swTotal = Stopwatch.StartNew();

            var searchResults = escli.Search<Entry>(s =>
                s.Query(qry =>
                    qry.Bool(b =>
                        b.Filter(f =>
                            f.Prefix(p =>
                                p.Field(e => e.Path).Value(string.Format(@"{0}\", curDir))
                            )
                        ).Must(m =>
                            m.QueryString(qs =>
                                qs.Fields(f => 
                                    f.Field(e => e.Content)
                                ).Query(key).DefaultOperator(Operator.And)
                            )
                        )
                    )
                ).Size(100).Sort(sort => 
                    sort.Ascending(e => e.Path)
                )
            ).Documents;

            foreach(var r in searchResults)
            {
                FilesListView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    FilesListView.Items.Add(new FileItemInfo(GetRealFileName(r.Path), r.IsDir ? "Directory" : "File", r.Path));
                }));
            }

            swTotal.Stop();
            TR.Total = swTotal.Elapsed.TotalMilliseconds * 1e6;
            MessageBox.Show(string.Format("Time Used: {0} s", swTotal.Elapsed.TotalSeconds));
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
            return curDir + ":" + key;
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
        //private void DispElapsedTime()
        //{
        //    Lbl_Used_Time.Content = string.Format("{0}s", stopwatch.Elapsed.TotalSeconds);
        //}
    }
}
