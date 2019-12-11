using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
        // 当前目录
        private string curDirPath;

        // 缓存列表
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
        }

        /// <summary>
        /// 连接redis
        /// </summary>
        private void ConnectRedis()
        {
            try
            {
                RedisHelper.SetCon(redisConnStr);
                MessageBox.Show("连接redis成功");
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
                curDirPath = folderBrowserDialog.SelectedPath;
                string[] files = Directory.GetFiles(curDirPath, "*.*");
                string[] subdirs = Directory.GetDirectories(curDirPath);

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
            stopwatch.Start();

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
            string combinedKey = GetCombinedKey(key);
            matchedFilenameList.Clear();
            if (RedisHelper.Exists(combinedKey))
            {
                matchedFilenameList = RedisHelper.Get<List<string>>(combinedKey);
                DispMatchedFiles();
                return;
            }

            string[] subdirs = Directory.GetDirectories(dir);
            foreach (var subdir in subdirs)
            {
                // 递归搜索文件夹
                SearchInSelectedDir(subdir, key);
            }

            ZipFiles();
            SearchPDF(key);
            SearchDoc(key);
            SearchImage(key);
            // 写回缓存
            if (matchedFilenameList.Count >0)
            {
                RedisHelper.Set(combinedKey, matchedFilenameList);
                DispMatchedFiles();
            } else
            {
                // 未搜索到匹配文件
                DispNotMatched();
            }
        }

        private void ZipFiles()
        {
            var files = Directory.GetFiles(curDirPath, "*.zip", SearchOption.AllDirectories);
            var zipper = new Ziper();
            foreach (var file in files)
            {
                var target = file.Substring(0, file.LastIndexOf('\\'));
                zipper.extract(file, target);
            }
        }


        // todo 未递归子目录搜索, 未递归压缩文件
        // todo 后期只遍历一次, 不分多次遍历; 前期分开调试用
        private void SearchImage(string key)
        {
            var files = Directory.GetFiles(curDirPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".bmp") || s.EndsWith(".jpg") || s.EndsWith(".png") || s.EndsWith(".jpeg"));
            foreach (var filename in files)
            {
                Console.WriteLine(filename);

            }

        }

        private void SearchDoc(string key)
        {
            var files = Directory.GetFiles(curDirPath, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".doc") || s.EndsWith(".docx"));
            foreach (var filename in files)
            {
                Console.WriteLine(filename);
            }
        }

        private void SearchPDF(string key)
        {
            string[] files = Directory.GetFiles(curDirPath, "*.pdf");
            
            foreach (var filename in files)
            {
                Console.WriteLine(filename);
                if(PDFContainsKey(filename, key))
                {
                    // 加入待显示列表
                    matchedFilenameList.Add(filename);
                }
            }
        }

        /// <summary>
        /// 调用外部函数
        /// </summary>
        /// <param name="filename">待匹配文件</param>
        /// <param name="key">待搜索键</param>
        /// <returns></returns>
        private bool PDFContainsKey(string filename, string key)
        {
            return false;
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

        /// <summary>
        /// 未搜索到提示
        /// </summary>
        private void DispNotMatched()
        {
            throw new NotImplementedException();
        }

    }
}
