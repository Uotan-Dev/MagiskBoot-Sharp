namespace Compress
{
    /// <summary>
    /// 提供自动识别格式的压缩和解压缩功能的主类
    /// </summary>
    public static class CompressionManager
    {
        #region 自动解压缩接口

        /// <summary>
        /// 自动识别格式并解压缩数据
        /// </summary>
        /// <param name="inputData">要解压的数据</param>
        /// <returns>解压后的数据，如果解压失败则返回null</returns>
        public static byte[]? AutoDecompress(byte[] inputData)
        {
            if (inputData == null || inputData.Length == 0)
                return null;

            // 检测格式
            var format = FormatUtils.CheckFormat(inputData, inputData.Length);

            // 如果为未知格式，返回null
            if (format == Format.UNKNOWN)
                return null;

            using var inputStream = new MemoryStream(inputData);
            using var outputStream = new MemoryStream();

            // 根据识别的格式选择合适的解压方法
            bool success = false;
            switch (format)
            {
                case Format.GZIP:
                    success = GzipUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.XZ:
                    success = XZUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.LZMA:
                    success = LzmaUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.BZIP2:
                    success = BZip2Utils.Decompress(inputStream, outputStream);
                    break;
                case Format.LZ4:
                    using (var ms = new MemoryStream(inputData))
                    {
                        return LZ4Compressor.AutoDecompress(inputData);
                    }
                case Format.LZ4_LEGACY:
                    return LZ4Compressor.Decompress(inputData, LZ4Compressor.LZ4Format.Legacy);
                case Format.LZ4_LG:
                    return LZ4Compressor.Decompress(inputData, LZ4Compressor.LZ4Format.LegacyWithSize);
                default:
                    return null;
            }

            return success ? outputStream.ToArray() : null;
        }

        /// <summary>
        /// 自动识别格式并解压缩流
        /// </summary>
        /// <param name="inputStream">要解压的输入流</param>
        /// <param name="outputStream">解压结果的输出流</param>
        /// <returns>解压是否成功</returns>
        public static bool AutoDecompress(Stream inputStream, Stream outputStream)
        {
            if (inputStream == null || outputStream == null || !inputStream.CanRead || !outputStream.CanWrite)
                return false;

            // 保存当前位置以便回退
            long originalPosition = 0;
            if (inputStream.CanSeek)
                originalPosition = inputStream.Position;

            // 读取前4KB用于格式检测
            byte[] buffer = new byte[4096];
            int bytesRead = inputStream.Read(buffer, 0, buffer.Length);

            // 重置流位置
            if (inputStream.CanSeek)
                inputStream.Position = originalPosition;
            else
                return false; // 如果不能回退位置，无法继续

            // 检测格式
            var format = FormatUtils.CheckFormat(buffer, bytesRead);

            // 根据格式选择解压方法
            switch (format)
            {
                case Format.GZIP:
                    return GzipUtils.Decompress(inputStream, outputStream);
                case Format.XZ:
                    return XZUtils.Decompress(inputStream, outputStream);
                case Format.LZMA:
                    return LzmaUtils.Decompress(inputStream, outputStream);
                case Format.BZIP2:
                    return BZip2Utils.Decompress(inputStream, outputStream);
                case Format.LZ4:
                case Format.LZ4_LEGACY:
                case Format.LZ4_LG:
                    // 对于LZ4格式，需要将整个流读入内存
                    using (var ms = new MemoryStream())
                    {
                        inputStream.CopyTo(ms);
                        byte[] compressedData = ms.ToArray();
                        byte[] decompressedData;

                        if (format == Format.LZ4)
                            decompressedData = LZ4Compressor.AutoDecompress(compressedData);
                        else if (format == Format.LZ4_LEGACY)
                            decompressedData = LZ4Compressor.Decompress(compressedData, LZ4Compressor.LZ4Format.Legacy);
                        else // LZ4_LG
                            decompressedData = LZ4Compressor.Decompress(compressedData, LZ4Compressor.LZ4Format.LegacyWithSize);

                        if (decompressedData != null)
                        {
                            outputStream.Write(decompressedData, 0, decompressedData.Length);
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 自动识别格式并解压缩文件
        /// </summary>
        /// <param name="inputFile">输入文件路径</param>
        /// <param name="outputFile">输出文件路径</param>
        /// <returns>解压是否成功</returns>
        public static bool AutoDecompressFile(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
                return false;

            using var inputStream = File.OpenRead(inputFile);
            using var outputStream = File.Create(outputFile);
            return AutoDecompress(inputStream, outputStream);
        }

        /// <summary>
        /// 自动识别格式并解压缩文件，自动生成输出文件名
        /// </summary>
        /// <param name="inputFile">输入文件路径</param>
        /// <returns>成功则返回输出文件路径，失败返回null</returns>
        public static string? AutoDecompressFile(string inputFile)
        {
            // 读取文件的前4KB用于格式检测
            byte[] buffer = new byte[4096];
            using (var fs = File.OpenRead(inputFile))
            {
                fs.ReadExactly(buffer);
            }

            // 检测格式
            var format = FormatUtils.CheckFormat(buffer, buffer.Length);
            if (format == Format.UNKNOWN)
                return null;

            // 生成输出文件名
            string extension = Path.GetExtension(inputFile);
            string outputFile;

            // 如果文件扩展名匹配检测到的格式，去掉扩展名
            if (!string.IsNullOrEmpty(extension) &&
                extension.Equals(FormatUtils.FormatToExtension(format), StringComparison.OrdinalIgnoreCase))
            {
                outputFile = Path.Combine(
                    Path.GetDirectoryName(inputFile) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(inputFile));
            }
            else
            {
                // 否则添加.decompressed后缀
                outputFile = inputFile + ".decompressed";
            }

            // 执行解压缩
            if (AutoDecompressFile(inputFile, outputFile))
                return outputFile;

            return null;
        }

        #endregion

        #region 自动压缩接口

        /// <summary>
        /// 压缩数据，根据指定的格式或从输出文件名推断格式
        /// </summary>
        /// <param name="inputData">要压缩的数据</param>
        /// <param name="format">压缩格式（可选）</param>
        /// <returns>压缩后的数据，如果压缩失败则返回null</returns>
        public static byte[]? Compress(byte[] inputData, Format format = Format.UNKNOWN)
        {
            if (inputData == null || inputData.Length == 0)
                return null;

            // 如果格式未指定或为UNKNOWN，默认使用GZIP
            if (format == Format.UNKNOWN)
                format = Format.GZIP;

            using var inputStream = new MemoryStream(inputData);
            using var outputStream = new MemoryStream();

            // 根据格式选择合适的压缩方法
            bool success = false;
            switch (format)
            {
                case Format.GZIP:
                case Format.ZOPFLI: // zopfli实际上是改进的gzip算法
                    success = GzipUtils.Compress(inputStream, outputStream);
                    break;
                case Format.XZ:
                    success = XZUtils.Compress(inputStream, outputStream);
                    break;
                case Format.LZMA:
                    success = LzmaUtils.Compress(inputStream, outputStream);
                    break;
                case Format.BZIP2:
                    success = BZip2Utils.Compress(inputStream, outputStream);
                    break;
                case Format.LZ4:
                    return LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.Frame);
                case Format.LZ4_LEGACY:
                    return LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.Legacy);
                case Format.LZ4_LG:
                    return LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.LegacyWithSize);
                default:
                    return null;
            }

            return success ? outputStream.ToArray() : null;
        }

        /// <summary>
        /// 压缩流，根据指定的格式
        /// </summary>
        /// <param name="inputStream">要压缩的输入流</param>
        /// <param name="outputStream">压缩结果的输出流</param>
        /// <param name="format">压缩格式</param>
        /// <returns>压缩是否成功</returns>
        public static bool Compress(Stream inputStream, Stream outputStream, Format format = Format.UNKNOWN)
        {
            if (inputStream == null || outputStream == null || !inputStream.CanRead || !outputStream.CanWrite)
                return false;

            // 如果格式未指定或为UNKNOWN，默认使用GZIP
            if (format == Format.UNKNOWN)
                format = Format.GZIP;

            // 根据格式选择压缩方法
            switch (format)
            {
                case Format.GZIP:
                case Format.ZOPFLI:
                    return GzipUtils.Compress(inputStream, outputStream);
                case Format.XZ:
                    return XZUtils.Compress(inputStream, outputStream);
                case Format.LZMA:
                    return LzmaUtils.Compress(inputStream, outputStream);
                case Format.BZIP2:
                    return BZip2Utils.Compress(inputStream, outputStream);
                case Format.LZ4:
                case Format.LZ4_LEGACY:
                case Format.LZ4_LG:
                    // 对于LZ4格式，需要将整个流读入内存
                    using (var ms = new MemoryStream())
                    {
                        inputStream.CopyTo(ms);
                        byte[] inputData = ms.ToArray();
                        byte[] compressedData;

                        if (format == Format.LZ4)
                            compressedData = LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.Frame);
                        else if (format == Format.LZ4_LEGACY)
                            compressedData = LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.Legacy);
                        else // LZ4_LG
                            compressedData = LZ4Compressor.Compress(inputData, LZ4Compressor.LZ4Format.LegacyWithSize);

                        outputStream.Write(compressedData, 0, compressedData.Length);
                        return true;
                    }
                default:
                    return false;
            }
        }

        /// <summary>
        /// 压缩文件，根据指定的格式或从输出文件名推断格式
        /// </summary>
        /// <param name="inputFile">输入文件路径</param>
        /// <param name="outputFile">输出文件路径，如果为null则自动生成</param>
        /// <param name="format">压缩格式，如果为UNKNOWN则尝试从输出文件名推断</param>
        /// <returns>成功则返回输出文件路径，失败返回null</returns>
        public static string? CompressFile(string inputFile, string? outputFile = null, Format format = Format.UNKNOWN)
        {
            if (!File.Exists(inputFile))
                return null;

            // 如果未提供输出文件名，则使用输入文件名 + 格式扩展名
            if (string.IsNullOrEmpty(outputFile))
            {
                // 如果格式未指定，默认使用GZIP
                if (format == Format.UNKNOWN)
                    format = Format.GZIP;

                outputFile = inputFile + FormatUtils.FormatToExtension(format);
            }
            // 如果提供了输出文件名但未指定格式，尝试从文件扩展名推断
            else if (format == Format.UNKNOWN)
            {
                string extension = Path.GetExtension(outputFile);
                if (!string.IsNullOrEmpty(extension))
                {
                    // 移除开头的点
                    if (extension.StartsWith("."))
                        extension = extension[1..];

                    // 尝试根据扩展名确定格式
                    format = extension.ToLower() switch
                    {
                        "gz" => Format.GZIP,
                        "xz" => Format.XZ,
                        "lzma" => Format.LZMA,
                        "bz2" => Format.BZIP2,
                        "lz4" => Format.LZ4,
                        _ => Format.GZIP,// 默认使用GZIP
                    };
                }
                else
                {
                    format = Format.GZIP; // 默认使用GZIP
                }
            }

            // 执行压缩
            using var inputStream = File.OpenRead(inputFile);
            using var outputStream = File.Create(outputFile);

            bool success = Compress(inputStream, outputStream, format);
            return success ? outputFile : null;
        }

        #endregion
    }
}