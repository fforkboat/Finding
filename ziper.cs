using System;
using System.Collections;
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
        private string removeFile;
        private Dictionary<string, string> table;

        public Dictionary<string, string> Table { get; set; }

        public Ziper()
        {
            table = new Dictionary<string, string>();
        }

        /// <summary>
        /// 删除解压后的文件
        /// </summary>
        public void ClearFile()
        {
            new DirectoryInfo(removeFile).Delete(true);
        }

        /// <summary>
        /// 解压缩函数
        /// </summary>
        /// <param name="sourceFile">目标路径</param>
        /// <param name="targetFile">解压后文件存放路径</param>
        /// returns zips' names which are processed from @sourceFile dir
        public List<string> extract(string sourceFile)
        {
            var zipnames = new List<string>();
            var files = Directory.GetFiles(sourceFile, "*.zip", SearchOption.AllDirectories);
            zipnames.AddRange(files);
            var target = sourceFile + @"\" + Guid.NewGuid();
            removeFile = target;
            foreach (var file in files)
            {
                zipnames.AddRange(ExtractFile(file, target, file));
            }
            return zipnames;
        }

        /// <summary>
        /// 解压缩函数
        /// </summary>
        /// <param name="sourceFile">文件流</param>
        /// <param name="targetFile">解压后文件存放路径</param>
        /// returns zips' names which are processed from @sourceFile dir
        public List<string> extract(FileStream sourceFile)
        {
            return extract(sourceFile.Name);
        }

        /// returns child zips' names which are processed from @sourceFileFullPath zip file
        private List<string> ExtractFile(string sourceFileFullPath, string targetFolderPath, string originalPath)
        {
            var zipnames = new List<string>();
            try
            {
                var encoding = Encoding.Default;
                var options = new ReadOptions { Encoding = encoding };
                using (var zip = ZipFile.Read(sourceFileFullPath, options))
                {
                    zip.AlternateEncoding = encoding;
                    zip.ExtractAll(targetFolderPath, ExtractExistingFileAction.OverwriteSilently); //一次批量解压
                    foreach (var s in zip.EntryFileNames)
                        if (s.EndsWith(".zip"))
                        {
                            //递归去解决其子文件
                            var source = targetFolderPath + @"\" + s;
                            zipnames.AddRange(ExtractFile(source, targetFolderPath, originalPath));
                        }
                        else
                        {
                            if (!s.EndsWith("/"))
                            {
                                table.Add(targetFolderPath + @"\" + s, originalPath);
                            }
                        }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
            return zipnames;
        }
    }
}