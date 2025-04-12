using Joveler.Compression.XZ;

namespace XZLibrary
{
    public class XZUtils
    {
        /// <summary>
        /// 使用XZ格式压缩数据从输入流到输出流
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

            // 创建XZ压缩选项,设定最小压缩体积
            var options = new XZCompressOptions
            {
                Level = LzmaCompLevel.Level9,
                Check = LzmaCheck.Crc32
            };

            // 创建XZ压缩流
            using var xzStream = new XZStream(output, options);

            // 将输入流复制到XZ压缩流
            input.CopyTo(xzStream);

            // 确保所有数据被写入
            xzStream.Flush();

            return true;
            // 故意不捕获具体异常，以便调用者可以处理
        }

        /// <summary>
        /// 解压缩XZ格式的数据从输入流到输出流
        /// </summary>
        /// <param name="input">要解压缩的XZ格式输入流</param>
        /// <param name="output">解压后的输出流</param>
        /// <returns>解压是否成功</returns>
        public static bool Decompress(Stream input, Stream output)
        {
            if (input == null || output == null)
                return false;

            if (!input.CanRead || !output.CanWrite)
                return false;

            // 创建XZ解压选项
            var options = new XZDecompressOptions();

            // 创建XZ解压流
            using var xzStream = new XZStream(input, options);

            // 将XZ解压流复制到输出流
            xzStream.CopyTo(output);

            return true;

            // 故意不捕获具体异常，以便调用者可以处理

        }
    }
}
