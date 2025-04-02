using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cpio
{
    /// <summary>
    /// 定义 CPIO 文件类型常量
    /// </summary>
    public static class CpioFileType
    {
        /// <summary>文件类型掩码</summary>
        public const uint S_IFMT = 0170000;

        /// <summary>普通文件</summary>
        public const uint S_IFREG = 0100000;

        /// <summary>目录</summary>
        public const uint S_IFDIR = 0040000;

        /// <summary>符号链接</summary>
        public const uint S_IFLNK = 0120000;

        /// <summary>块设备</summary>
        public const uint S_IFBLK = 0060000;

        /// <summary>字符设备</summary>
        public const uint S_IFCHR = 0020000;

        // 权限位常量
        public const uint S_IRUSR = 0400;  // 用户读权限
        public const uint S_IWUSR = 0200;  // 用户写权限  
        public const uint S_IXUSR = 0100;  // 用户执行权限

        public const uint S_IRGRP = 0040;  // 组读权限
        public const uint S_IWGRP = 0020;  // 组写权限
        public const uint S_IXGRP = 0010;  // 组执行权限

        public const uint S_IROTH = 0004;  // 其他人读权限
        public const uint S_IWOTH = 0002;  // 其他人写权限
        public const uint S_IXOTH = 0001;  // 其他人执行权限
    }
}
