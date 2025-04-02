using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Cpio.Helpers
{
    /// <summary>
    /// 文件操作辅助类
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// 创建符号链接
        /// </summary>
        /// <param name="symlinkPath">符号链接路径</param>
        /// <param name="targetPath">目标路径</param>
        /// <returns>是否创建成功</returns>
        /// <remarks>
        /// 此方法仅在 Windows Vista 及以上版本支持
        /// 并且需要管理员权限或已启用开发者模式
        /// </remarks>
        public static bool CreateSymbolicLink(string symlinkPath, string targetPath)
        {
            // 目标是文件则 dwFlags=0，目标是目录则 dwFlags=1
            int dwFlags = 0;

            if (!CreateSymbolicLink(symlinkPath, targetPath, dwFlags))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception($"Failed to create symbolic link. Error code: {error}");
            }
            return true;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);
    }
}
