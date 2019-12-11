using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ionic.Zip;

namespace Finding
{
    /// <summary>
    /// 解压
    /// </summary>
    public class Ziper
    {
        private List<string> docxList;
        private List<string> imageList;

        public Ziper()
        {
            docxList = new List<string>();
            imageList = new List<string>();
        }

        /// <summary>
        /// 获得递归解压后文件中的doc、pdf等文件的绝对路径
        /// </summary>
        /// <returns>文件路径的List</returns>
        public List<string> GetDocxList()
        {
            return docxList;
        }

        /// <summary>
        /// 获得递归解压后文件中的image等文件的绝对路径
        /// </summary>
        /// <returns>照片路径的List</returns>
        public List<string> GetImageList()
        {
            return imageList;
        }

        /// <summary>
        /// 解压缩函数
        /// </summary>
        /// <param name="sourceFile">目标路径</param>
        /// <param name="targetFile">解压后文件存放路径</param>
        public void extract(string sourceFile, string targetFile)
        {
            List<string> list = ExtractFile(sourceFile, targetFile);
            if (list != null)
            {
                init(list);
            }
        }

        /// <summary>
        /// 解压缩函数
        /// </summary>
        /// <param name="sourceFile">文件流</param>
        /// <param name="targetFile">解压后文件存放路径</param>
        public void extract(FileStream sourceFile, string targetFile)
        {
            List<string> list = ExtractFile(sourceFile.Name, targetFile);
            if (list != null)
            {
                init(list);
            }
        }

        private List<string> ExtractFile(string sourceFileFullPath, string targetFolderPath)
        {
            try
            {
                var encoding = Encoding.Default;
                var options = new ReadOptions { Encoding = encoding };
                List<string> filesInfo = new List<string>();
                using (var zip = ZipFile.Read(sourceFileFullPath, options))
                {
                    zip.AlternateEncoding = encoding;
                    zip.ExtractAll(targetFolderPath, ExtractExistingFileAction.OverwriteSilently); //一次批量解压
                    foreach (var s in zip.EntryFileNames)
                    {
                        int index = s.IndexOf(".zip", StringComparison.Ordinal);
                        if (index == -1)
                        {
                            filesInfo.Add(targetFolderPath + '\\' + s);
                        }
                        else
                        {
                            //
                            string file = s.Substring(0, s.LastIndexOf('/'));
                            //递归去解决其子文件
                            string source = targetFolderPath + '\\' + s;
                            string target = targetFolderPath + '\\' + file;
                            List<string> strs = ExtractFile(source, target);
                            if (strs != null)
                            {
                                filesInfo.AddRange(strs);
                            }
                        }
                    }
                    return filesInfo;
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
                return null;
            }
        }

        private void init(List<string> filesInfo)
        {
            foreach (string name in filesInfo)
            {
                if (isImage(name))
                {
                    imageList.Add(name);
                }
                else if (isDocx(name))
                {
                    docxList.Add(name);
                }
            }
        }

        private bool isImage(string file)
        {
            return file.Equals(".jpg") || file.EndsWith(".png")
                                       || file.EndsWith(".gif") || file.EndsWith(".jpeg");
        }

        private bool isDocx(string file)
        {
            return file.EndsWith(".pdf") || file.EndsWith(".doc") 
                || file.EndsWith(".excel") || file.EndsWith(".ppt") || file.EndsWith(".txt");
        }
    }
}
