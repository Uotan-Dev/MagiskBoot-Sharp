using SharpCompress.Compressors.LZMA;

namespace LZMALibrary
{
    public static class LzmaUtils
    {
        /// <summary>
        /// 使用LZMA算法压缩输入流到输出流
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

            // 设置最大压缩编码器属性
            var properties = new LzmaEncoderProperties(
                                                        true,      // 是否使用末尾标记
                                                        64 << 20,  // 64MB 字典大小
                                                        273        // 273个快速字节
                                                        );

            // 创建LZMA压缩流，isLzma2=true表示使用LZMA2格式
            using var lzmaStream = new LzmaStream(properties, true, output);
            // 将输入流复制到LZMA流中进行压缩
            input.CopyTo(lzmaStream);
            // 确保所有数据都被写入并正确关闭
            lzmaStream.Close();
            return true;

        }

        /// <summary>
        /// 解压缩LZMA格式的输入流到输出流
        /// </summary>
        /// <param name="input">要解压缩的LZMA格式输入流</param>
        /// <param name="output">解压后的输出流</param>
        /// <returns>解压是否成功</returns>
        public static bool Decompress(Stream input, Stream output)
        {
            if (input == null || output == null)
                return false;

            if (!input.CanRead || !output.CanWrite)
                return false;

            // 读取LZMA头部属性（LZMA格式前5个字节是属性信息）
            var properties = new byte[5];
            if (input.Read(properties, 0, 5) != 5)
                return false;

            // 读取可选的输出大小信息（8字节），可能不存在于所有LZMA流中
            long decompressedSize = -1;

            var sizeBytes = new byte[8];
            if (input.Read(sizeBytes, 0, 8) == 8)
            {
                decompressedSize = BitConverter.ToInt64(sizeBytes, 0);
                if (decompressedSize == -1)
                    decompressedSize = -1;
            }


            // 创建LZMA解压流
            using var lzmaStream = new LzmaStream(properties, input, input.Length - input.Position, decompressedSize);
            // 将解压后的数据复制到输出流
            lzmaStream.CopyTo(output);

            return true;

        }

        /// <summary>
        /// 使用自定义字典大小和字节数压缩流
        /// </summary>
        public static bool CompressAdvanced(Stream input, Stream output, int dictionarySize = 1 << 20, int numFastBytes = 32)
        {
            // 创建自定义编码器属性
            var properties = new LzmaEncoderProperties(true, dictionarySize, numFastBytes);

            using var lzmaStream = new LzmaStream(properties, false, output);
            input.CopyTo(lzmaStream);
            lzmaStream.Close();

            return true;
        }
    }
}

