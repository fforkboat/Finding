using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Finding
{
    class MD5Checker
    {
        /// <summary>
        /// 计算文件的MD5摘要
        /// </summary>
        /// <param name="path">文件地址</param>
        /// <returns>MD5Hash</returns>
        public static string GetFileMD5(string path)
        {
            if (!File.Exists(path))
                throw new ArgumentException(string.Format("{0}不存在", path));
            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider();
            byte[] buffer = md5Provider.ComputeHash(fs);
            string res = BitConverter.ToString(buffer);
            res = res.Replace("-", "");
            md5Provider.Clear();
            fs.Close();
            return res;
        }
    }
}
