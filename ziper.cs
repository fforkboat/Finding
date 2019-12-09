using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;

namespace Finding
{
    /*
        * function: void extract()
        * param1: 需要解压缩的文件
        * param2: 解压后新建的目标文件夹名
        * (get)docxList: pdf\doc\excel\ppt\txt 文本文件
        * (get)imageList：jpg\png\gif 图片文件
        */
    public class Ziper
    {
        private List<string> docxList;
        private List<string> imageList;

        public Ziper()
        {
            docxList = new List<string>();
            imageList = new List<string>();
        }

        public List<string> getDocxList()
        {
            return docxList;
        }

        public List<string> getImageeList()
        {
            return imageList;
        }

        public void extract(string sourceFile, string targetFile)
        {
            List<string> list = ExtractFile(sourceFile, targetFile);
            if (list != null)
            {
                init(list);
            }
        }

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
            int index = file.LastIndexOf('.');
            string fix = file.Substring(index + 1);
            if (fix.Equals("jpg") || fix.Equals("png") || fix.Equals("gif"))
            {
                return true;
            }
            return false;

        }

        private bool isDocx(string file)
        {
            int index = file.LastIndexOf('.');
            string fix = file.Substring(index + 1);
            if (fix.Equals("pdf") || fix.Equals("doc") || fix.Equals("excel") || fix.Equals("ppt") || fix.Equals("txt"))
            {
                return true;
            }
            return false;
        }
    }
}
