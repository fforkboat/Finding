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
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        // redis配置
        private const string redisConnStr = "127.0.0.1:6379,password=,DefaultDatabase=0";

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
        }

        // 点击 OpenDirectoryMenuItem 的事件处理函数，用于打开一个文件夹
        private void OpenDirectoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog folderBrowserDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();

            if (folderBrowserDialog.ShowDialog() == true)
            {
                String path = folderBrowserDialog.SelectedPath;
                string[] files = Directory.GetFiles(path, "*.*");
                string[] subdirs = Directory.GetDirectories(path);

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

            SearchInSelectedDir(Txb_Search_Key.Text);

            stopwatch.Stop();
            DispElapsedTime();
        }

        /// <summary>
        /// 根据key搜索当前目录下匹配的文件
        /// </summary>
        /// <param name="key">搜索关键字</param>
        private void SearchInSelectedDir(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 显示搜索耗时
        /// </summary>
        private void DispElapsedTime()
        {
            // 
            Lbl_Used_Time.Content = string.Format("{0}s", stopwatch.Elapsed.TotalSeconds);
            
        }
    }
}
