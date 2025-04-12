using System.IO.Compression;


namespace GzipLibrary
{
    public class GzipUtils
    {
        /// <summary>
        /// 使用Gzip压缩流数据
        /// </summary>
        /// <param name="input">要压缩的输入流</param>
        /// <param name="output">压缩后的输出流</param>
        /// <returns>压缩是否成功</returns>
        public static bool Compress(Stream input, Stream output)
        {
            try
            {
                // 确保输入流可读取
                if (!input.CanRead)
                    return false;

                // 确保输出流可写入
                if (!output.CanWrite)
                    return false;

                using var gzipStream = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
                input.CopyTo(gzipStream);
                gzipStream.Flush();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 解压缩Gzip流数据
        /// </summary>
        /// <param name="input">要解压的输入流</param>
        /// <param name="output">解压后的输出流</param>
        /// <returns>解压是否成功</returns>
        public static bool Decompress(Stream input, Stream output)
        {
            try
            {
                // 确保输入流可读取
                if (!input.CanRead)
                    return false;

                // 确保输出流可写入
                if (!output.CanWrite)
                    return false;

                using var gzipStream = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
                gzipStream.CopyTo(output);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}