using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace Compress
{
    public class BZip2Utils
    {
        /// <summary>
        /// 将数据从输入流压缩到输出流，使用BZip2格式
        /// </summary>
        /// <param name="input">要压缩的输入流</param>
        /// <param name="output">压缩后的输出流</param>
        /// <returns>压缩是否成功</returns>
        public static bool Compress(Stream input, Stream output)
        {
            if (input == null || output == null)
                return false;

            if (!input.CanRead || !output.CanWrite)
                return false;

            using var bzip2Stream = new BZip2Stream(output, CompressionMode.Compress, false);
            input.CopyTo(bzip2Stream);
            // 调用Finish确保所有数据被写入并正确结束压缩流
            bzip2Stream.Finish();
            // 故意未使用try-catch捕获异常，以允许调用者处理，例如让工具箱弹窗
            return true;
        }


        /// <summary>
        /// 将BZip2格式的数据从输入流解压到输出流
        /// </summary>
        /// <param name="input">要解压缩的BZip2格式输入流</param>
        /// <param name="output">解压后的输出流</param>
        /// <param name="decompressConcatenated">是否解压连接的多个BZip2流，默认为Flase</param>
        /// <returns>解压是否成功</returns>
        public static bool Decompress(Stream input, Stream output, bool decompressConcatenated = false)
        {
            if (input == null || output == null)
                return false;

            if (!input.CanRead || !output.CanWrite)
                return false;

            // 虽然只有FrormatCheck检测之后才会调用这个方法，但还是检查输入流是否是BZip2格式
            if (!BZip2Stream.IsBZip2(input))
            {
                // 如果进行了检查后流指针已经移动，需要重置
                if (input.CanSeek)
                    input.Seek(0, SeekOrigin.Begin);
                else
                    return false; // 无法重置流位置且不是BZip2格式
            }

            using var bzip2Stream = new BZip2Stream(input, CompressionMode.Decompress, decompressConcatenated);
            bzip2Stream.CopyTo(output);
            return true;
        }
    }
}