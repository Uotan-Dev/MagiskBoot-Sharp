using Cpio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cpio.Extensions
{
    /// <summary>
    /// CpioEntry 类的扩展方法
    /// </summary>
    public static class CpioEntryExtensions
    {
        /// <summary>
        /// 格式化 CPIO 条目的文件模式为 ls -l 风格的输出
        /// </summary>
        /// <param name="entry">要格式化的条目</param>
        /// <returns>格式化后的字符串</returns>
        public static string FormatFileMode(this CpioEntry entry)
        {
            char type;
            uint mode = entry.Mode;

            // 确定文件类型
            switch (mode & CpioFileType.S_IFMT)
            {
                case CpioFileType.S_IFDIR: type = 'd'; break;
                case CpioFileType.S_IFREG: type = '-'; break;
                case CpioFileType.S_IFLNK: type = 'l'; break;
                case CpioFileType.S_IFBLK: type = 'b'; break;
                case CpioFileType.S_IFCHR: type = 'c'; break;
                default: type = '?'; break;
            }

            // 格式化权限位
            char ur = ((mode & CpioFileType.S_IRUSR) != 0) ? 'r' : '-';
            char uw = ((mode & CpioFileType.S_IWUSR) != 0) ? 'w' : '-';
            char ux = ((mode & CpioFileType.S_IXUSR) != 0) ? 'x' : '-';

            char gr = ((mode & CpioFileType.S_IRGRP) != 0) ? 'r' : '-';
            char gw = ((mode & CpioFileType.S_IWGRP) != 0) ? 'w' : '-';
            char gx = ((mode & CpioFileType.S_IXGRP) != 0) ? 'x' : '-';

            char or = ((mode & CpioFileType.S_IROTH) != 0) ? 'r' : '-';
            char ow = ((mode & CpioFileType.S_IWOTH) != 0) ? 'w' : '-';
            char ox = ((mode & CpioFileType.S_IXOTH) != 0) ? 'x' : '-';

            string size = FormatSize(entry.Data.Length);

            return $"{type}{ur}{uw}{ux}{gr}{gw}{gx}{or}{ow}{ox}\t{entry.Uid}\t{entry.Gid}\t{size}\t{entry.RdevMajor}:{entry.RdevMinor}";
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        /// <param name="bytes">字节大小</param>
        /// <returns>格式化的大小字符串</returns>
        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "K", "M", "G", "T" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            if (order == 0)
                return $"{bytes}";
            else
                return $"{size:0.#}{suffixes[order]}";
        }
    }
}
