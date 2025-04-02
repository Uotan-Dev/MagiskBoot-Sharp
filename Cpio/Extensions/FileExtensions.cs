using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cpio.Extensions
{
    /// <summary>
    /// 文件相关的扩展方法
    /// </summary>
    public static class FileExtensions
    {
        /// <summary>
        /// 向流中写入指定数量的零字节
        /// </summary>
        /// <param name="stream">目标流</param>
        /// <param name="count">零字节数量</param>
        public static void WriteZeros(this Stream stream, int count)
        {
            if (count <= 0)
                return;

            byte[] zeros = new byte[count];
            stream.Write(zeros, 0, count);
        }
    }
}
