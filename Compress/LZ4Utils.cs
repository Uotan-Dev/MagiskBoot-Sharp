using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace Compress
{
    /// <summary>
    /// LZ4 压缩/解压缩实现
    /// </summary>
    public static class LZ4Compressor
    {
        private const int LZ4_UNCOMPRESSED = 0x800000; // 8MB
        private const int CHUNK_SIZE = 0x40000; // 256KB 
        public static readonly byte[] LZ4_MAGIC = [0x02, 0x21, 0x4C, 0x18];

        /// <summary>
        /// LZ4 压缩格式枚举
        /// </summary>
        public enum LZ4Format
        {
            /// <summary>标准LZ4 Frame格式</summary>
            Frame,
            /// <summary>传统LZ4格式</summary>
            Legacy,
            /// <summary>带总大小的LZ4格式</summary>
            LegacyWithSize
        }

        #region 压缩方法

        /// <summary>
        /// 压缩数据
        /// </summary>
        /// <param name="input">输入数据</param>
        /// <param name="format">LZ4格式</param>
        /// <returns>压缩后的数据</returns>
        public static byte[] Compress(byte[] input, LZ4Format format = LZ4Format.Frame)
        {
            return format switch
            {
                LZ4Format.Frame => CompressLZ4Frame(input),
                LZ4Format.Legacy => CompressLZ4Legacy(input, false),
                LZ4Format.LegacyWithSize => CompressLZ4Legacy(input, true),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }

        /// <summary>
        /// 使用LZ4 Frame格式压缩数据
        /// </summary>
        private static byte[] CompressLZ4Frame(byte[] input)
        {
            using var outputStream = new MemoryStream();
            using var lz4Stream = LZ4Stream.Encode(outputStream, new LZ4EncoderSettings
            {
                BlockSize = 4 * 1024 * 1024,  // LZ4F_max4MB
                ContentChecksum = true,       // 启用内容校验
                BlockChecksum = false,        // 不使用块校验
                ChainBlocks = false,          // 独立块
                CompressionLevel = LZ4Level.L09_HC // 最高压缩级别
            });

            lz4Stream.Write(input, 0, input.Length);
            lz4Stream.Close();
            return outputStream.ToArray();
        }

        /// <summary>
        /// 使用传统LZ4格式压缩数据
        /// </summary>
        /// <param name="input">输入数据</param>
        /// <param name="includeSize">是否包含原始数据大小信息</param>
        private static byte[] CompressLZ4Legacy(byte[] input, bool includeSize)
        {
            using var outputStream = new MemoryStream();

            // 写入Magic Number
            outputStream.Write(LZ4_MAGIC, 0, LZ4_MAGIC.Length);

            int offset = 0;
            while (offset < input.Length)
            {
                // 确定当前块大小
                int blockLength = Math.Min(LZ4_UNCOMPRESSED, input.Length - offset);
                byte[] blockData = new byte[blockLength];
                Buffer.BlockCopy(input, offset, blockData, 0, blockLength);

                // 压缩块
                byte[] compressedBlock = LZ4Pickler.Pickle(blockData, LZ4Level.L09_HC);

                // 写入压缩后块的大小
                byte[] blockSize = BitConverter.GetBytes(compressedBlock.Length);
                outputStream.Write(blockSize, 0, blockSize.Length);

                // 写入压缩后的数据
                outputStream.Write(compressedBlock, 0, compressedBlock.Length);

                offset += blockLength;
            }

            // 如果需要，写入原始总大小信息
            if (includeSize)
            {
                byte[] originalSize = BitConverter.GetBytes(input.Length);
                outputStream.Write(originalSize, 0, originalSize.Length);
            }

            return outputStream.ToArray();
        }

        #endregion

        #region 解压缩方法

        /// <summary>
        /// 解压缩数据
        /// </summary>
        /// <param name="input">压缩数据</param>
        /// <param name="format">LZ4格式</param>
        /// <returns>解压后的数据</returns>
        public static byte[] Decompress(byte[] input, LZ4Format format = LZ4Format.Frame)
        {
            return format switch
            {
                LZ4Format.Frame => DecompressLZ4Frame(input),
                LZ4Format.Legacy => DecompressLZ4Legacy(input),
                LZ4Format.LegacyWithSize => DecompressLZ4Legacy(input),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }

        /// <summary>
        /// 检测压缩格式并自动解压
        /// </summary>
        public static byte[] AutoDecompress(byte[] input)
        {
            // 检查魔数以确定格式
            if (input.Length >= 4)
            {
                // 检查是否为LZ4传统格式
                if (input[0] == LZ4_MAGIC[0] && input[1] == LZ4_MAGIC[1] &&
                    input[2] == LZ4_MAGIC[2] && input[3] == LZ4_MAGIC[3])
                {
                    return DecompressLZ4Legacy(input);
                }
                // 检查是否为LZ4 Frame格式 (魔数 04 22 4D 18)
                else if (input[0] == 0x04 && input[1] == 0x22 &&
                         input[2] == 0x4D && input[3] == 0x18)
                {
                    return DecompressLZ4Frame(input);
                }
            }
            throw new InvalidDataException("未识别的LZ4压缩格式");
        }

        /// <summary>
        /// 使用LZ4 Frame格式解压数据
        /// </summary>
        private static byte[] DecompressLZ4Frame(byte[] input)
        {
            using var inputStream = new MemoryStream(input);
            using var outputStream = new MemoryStream();
            using var lz4Stream = LZ4Stream.Decode(inputStream);

            byte[] buffer = new byte[CHUNK_SIZE];
            int read;
            while ((read = lz4Stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, read);
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// 使用传统LZ4格式解压数据
        /// </summary>
        private static byte[] DecompressLZ4Legacy(byte[] input)
        {
            using var inputStream = new MemoryStream(input);
            using var outputStream = new MemoryStream();

            // 跳过魔数
            inputStream.Position = 4;

            while (inputStream.Position < inputStream.Length)
            {
                // 读取块大小
                byte[] blockSizeBytes = new byte[4];
                if (inputStream.Read(blockSizeBytes, 0, 4) != 4)
                    break;

                int blockSize = BitConverter.ToInt32(blockSizeBytes, 0);

                // 检查是否为魔数
                if (blockSize == 0x184C2102)
                {
                    // 这是LZ4魔数，重新读取下一个块大小
                    if (inputStream.Read(blockSizeBytes, 0, 4) != 4)
                        break;

                    blockSize = BitConverter.ToInt32(blockSizeBytes, 0);
                }

                // 读取压缩块
                byte[] compressedBlock = new byte[blockSize];
                if (inputStream.Read(compressedBlock, 0, blockSize) != blockSize)
                    throw new InvalidDataException("无法读取完整的压缩块");

                // 解压块
                byte[] decompressedBlock = LZ4Pickler.Unpickle(compressedBlock);
                outputStream.Write(decompressedBlock, 0, decompressedBlock.Length);
            }

            return outputStream.ToArray();
        }

        #endregion

        #region 流处理扩展

        /// <summary>
        /// 创建LZ4压缩流
        /// </summary>
        public static Stream CreateCompressionStream(Stream outputStream, LZ4Format format = LZ4Format.Frame)
        {
            return format switch
            {
                LZ4Format.Frame => LZ4Stream.Encode(outputStream, new LZ4EncoderSettings
                {
                    BlockSize = 4 * 1024 * 1024,
                    ContentChecksum = true,
                    BlockChecksum = false,
                    ChainBlocks = false,
                    CompressionLevel = LZ4Level.L09_HC
                }),
                _ => new LZ4LegacyStream(outputStream, true, format == LZ4Format.LegacyWithSize)
            };
        }

        /// <summary>
        /// 创建LZ4解压缩流
        /// </summary>
        public static Stream CreateDecompressionStream(Stream inputStream, LZ4Format format = LZ4Format.Frame)
        {
            return format switch
            {
                LZ4Format.Frame => LZ4Stream.Decode(inputStream),
                _ => new LZ4LegacyStream(inputStream, false, format == LZ4Format.LegacyWithSize)
            };
        }

        #endregion
    }

    /// <summary>
    /// 处理传统LZ4格式的流
    /// </summary>
    internal class LZ4LegacyStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly bool _isCompress;
        private readonly bool _includeSize;
        private readonly MemoryStream _buffer;
        private int _totalSize;
        private readonly bool _headerWritten;
        private bool _isDisposed;

        public LZ4LegacyStream(Stream baseStream, bool isCompress, bool includeSize)
        {
            _baseStream = baseStream;
            _isCompress = isCompress;
            _includeSize = includeSize;
            _buffer = new MemoryStream();
            _totalSize = 0;
            _headerWritten = false;
            _isDisposed = false;

            if (_isCompress)
            {
                // 初始化时写入魔数
                _baseStream.Write(LZ4Compressor.LZ4_MAGIC, 0, 4);
            }
            else
            {
                // 解压模式读取并验证魔数
                byte[] magic = new byte[4];
                if (_baseStream.Read(magic, 0, 4) != 4 ||
                    magic[0] != 0x02 || magic[1] != 0x21 ||
                    magic[2] != 0x4C || magic[3] != 0x18)
                {
                    throw new InvalidDataException("无效的LZ4传统格式头");
                }
            }
        }

        public override bool CanRead => !_isCompress;
        public override bool CanSeek => false;
        public override bool CanWrite => _isCompress;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            if (_isCompress && _buffer.Length > 0)
            {
                ProcessBuffer();
            }
            _baseStream.Flush();
        }

        private void ProcessBuffer()
        {
            if (_buffer.Length == 0) return;

            byte[] data = _buffer.ToArray();
            byte[] compressed = LZ4Pickler.Pickle(data, LZ4Level.L09_HC);

            // 写入块大小
            byte[] blockSize = BitConverter.GetBytes(compressed.Length);
            _baseStream.Write(blockSize, 0, blockSize.Length);

            // 写入压缩数据
            _baseStream.Write(compressed, 0, compressed.Length);

            _totalSize += (int)_buffer.Length;
            _buffer.SetLength(0);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_isCompress) throw new NotSupportedException("压缩流不支持读取操作");

            // 读取块大小
            byte[] blockSizeBuffer = new byte[4];
            int read = _baseStream.Read(blockSizeBuffer, 0, 4);
            if (read != 4) return 0;

            int blockSize = BitConverter.ToInt32(blockSizeBuffer, 0);

            // 读取压缩块
            byte[] compressedBlock = new byte[blockSize];
            read = _baseStream.Read(compressedBlock, 0, blockSize);
            if (read != blockSize)
                throw new InvalidDataException("无法读取完整的压缩块");

            // 解压块
            byte[] decompressedBlock = LZ4Pickler.Unpickle(compressedBlock);

            // 复制到输出缓冲区
            int bytesToCopy = Math.Min(decompressedBlock.Length, count);
            Buffer.BlockCopy(decompressedBlock, 0, buffer, offset, bytesToCopy);

            return bytesToCopy;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_isCompress) throw new NotSupportedException("解压缩流不支持写入操作");

            _buffer.Write(buffer, offset, count);

            // 如果缓冲区达到阈值，则处理它
            if (_buffer.Length >= 0x800000)
            {
                ProcessBuffer();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    if (_isCompress)
                    {
                        // 处理剩余的缓冲区数据
                        ProcessBuffer();

                        // 如果需要，写入总大小
                        if (_includeSize)
                        {
                            byte[] totalSizeBytes = BitConverter.GetBytes(_totalSize);
                            _baseStream.Write(totalSizeBytes, 0, totalSizeBytes.Length);
                        }
                    }

                    _buffer.Dispose();
                }

                _isDisposed = true;
            }

            base.Dispose(disposing);
        }
    }
}