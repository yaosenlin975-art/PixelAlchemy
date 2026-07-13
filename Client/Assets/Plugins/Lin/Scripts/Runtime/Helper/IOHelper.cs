using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Lin.Runtime.Helper
{
    //文件操作辅助工具
    public static class IOHelper
    {
        //文件是否被占用
        public static bool IsOccupied(string path)
        {
            try
            {
                using FileStream file = File.Open(path, FileMode.Open);
                return false;
            }
            catch (System.Exception)
            {
                return true;
            }
        }

        /// <summary> 根据本地时间生成时间戳 </summary>
        public static string GetLocalTimeStamp()
        {
            var now = DateTime.Now;
            return $"{now.ToString("yyyyMMdd")}{now.Hour * 60 + now.Minute}";
        }

        /// <summary> 大小单位转换 B → MB </summary>
        public static float B2MB(long bytes) => bytes / 1024f / 1024f;

        /// <summary> 获取文件MD5码 </summary>
        public static string GetMD5(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                using (var crypto = MD5.Create())
                {
                    var md5 = crypto.ComputeHash(fs);
                    return md5.Aggregate(string.Empty, (res, b) => res = res + b.ToString("X2"));
                }
            }
        }
        /// <summary> 获取文件MD5码 </summary>
        public static string GetMD5(this Stream self)
        {
            using (var crypto = MD5.Create())
            {
                var md5 = crypto.ComputeHash(self);
                return md5.Aggregate(string.Empty, (res, b) => res = res + b.ToString("X2"));
            }
        }

        /// <summary> 获取文件MD5码 </summary>
        public static string GetMD5(this FileInfo self)
        {
            using (FileStream fs = self.OpenRead())
            {
                using (var crypto = MD5.Create())
                {
                    var md5 = crypto.ComputeHash(fs);
                    return md5.Aggregate(string.Empty, (res, b) => res = res + b.ToString("X2"));
                }
            }
        }

        public static string GetMD5(this DirectoryInfo self, string[] ignores)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                var files = Directory.GetFiles(self.FullName, "*", SearchOption.AllDirectories);
                Array.Sort(files);
                foreach (var file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (ignores.Contains(fileInfo.Extension))
                        continue;

                    byte[] buffer = File.ReadAllBytes(file);
                    stream.Write(buffer, 0, buffer.Length);
                }

                using (var crypto = MD5.Create())
                {
                    var md5 = crypto.ComputeHash(stream);
                    return md5.Aggregate(string.Empty, (res, b) => res = res + b.ToString("X2"));
                }
            }
        }

        /// <summary> 计算整个文件夹的大小 </summary>
        public static long GetSize(this DirectoryInfo self, string[] ignoreExtensions = null)
        {
            if (!self.Exists)
                return 0;

            long size = 0;
            size += self.GetFiles("*", SearchOption.AllDirectories).Sum(file =>
            {
                if (ignoreExtensions != null && ignoreExtensions.Contains(file.Extension))
                    return 0;

                return file.Length;
            });
            return size;
        }

        public static void InsureExist(string path, bool isFile, bool force = false)
        {
            if (isFile)
                new FileInfo(path).InsureExist(force);
            else
                new DirectoryInfo(path).InsureExist(force);
        }

        public static DirectoryInfo InsureExist(this DirectoryInfo self, bool recursive = false)
        {
            if (self.Exists)
            {
                if (recursive)
                {
                    self.Delete(true);
                    self.Create();
                    self.Refresh();
                }
                return self;
            }

            self.Create();
            self.Refresh();
            return self;
        }

        public static void InsureExist(this FileInfo self, bool force = false)
        {
            if (!self.Exists || force)
            {
                self.Directory.InsureExist(false);
                self.Create().Close();
                self.Refresh();
            }
        }

        public static string GetSizeString(long num)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (num > 1024 * 1024 && i < u.Length - 1)
            {
                num /= 1024;
                i++;
            }
            string res;
            if (num > 1024 && i < u.Length - 1)
            {
                i++;
                res = ((double)num / 1024).ToString("f2") + u[i];
                return res;
            }
            res = num.ToString("f2") + u[i];
            return res;
        }
    }
}