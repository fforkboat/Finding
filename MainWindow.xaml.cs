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
        private class FileItemInfo
        {
            public string Name { set; get; }
            public string Type { set; get; }
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

        private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var fileItemInfo = ((ListViewItem)sender).Content as FileItemInfo;
            System.Diagnostics.Process.Start(fileItemInfo.Path);
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
