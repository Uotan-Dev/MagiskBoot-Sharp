using Compress;

namespace BootImage
{
    /// <summary>
    /// 引导镜像解包器，用于提取和解压引导镜像中的组件
    /// </summary>
    public class BootImageUnpacker
    {
        /// <summary>
        /// 解包引导镜像文件
        /// </summary>
        /// <param name="imagePath">引导镜像文件路径</param>
        /// <param name="skipDecomp">是否跳过解压缩</param>
        /// <param name="extractHeader">是否提取头部信息</param>
        /// <returns>如果是ChromeOS镜像返回2，否则返回0</returns>
        public static int Unpack(string imagePath, bool skipDecomp = false, bool extractHeader = true)
        {
            using var bootImage = new BootImage(imagePath);

            // 导出头部信息
            if (extractHeader)
                bootImage.DumpHeaderToFile();

            // 解压并导出内核
            if (!skipDecomp && IsCompressed(bootImage.KernelFormat))
            {
                if (bootImage.Kernel != null && bootImage.Kernel.Length > 0)
                {
                    using var fs = File.Create(BootImage.KERNEL_FILE);
                    DecompressData(bootImage.Kernel, bootImage.KernelFormat, fs);
                }
            }
            else
            {
                if (bootImage.Kernel != null && bootImage.Kernel.Length > 0)
                    File.WriteAllBytes(BootImage.KERNEL_FILE, bootImage.Kernel);
            }

            // 导出内核DTB
            if (bootImage.KernelDtb != null && bootImage.KernelDtb.Length > 0)
                File.WriteAllBytes(BootImage.KER_DTB_FILE, bootImage.KernelDtb);

            // 解压并导出ramdisk
            if (bootImage.HasVendorRamdiskTable)
            {
                // 处理多个vendor ramdisks
                Directory.CreateDirectory(BootImage.VND_RAMDISK_DIR);

                foreach (var entry in bootImage.VendorRamdiskEntries)
                {
                    string fileName = string.IsNullOrEmpty(entry.RamdiskName) ?
                        BootImage.RAMDISK_FILE : $"{entry.RamdiskName}.cpio";
                    string filePath = Path.Combine(BootImage.VND_RAMDISK_DIR, fileName);

                    byte[] ramdiskData = new byte[entry.RamdiskSize];
                    Buffer.BlockCopy(bootImage.Ramdisk, entry.RamdiskOffset, ramdiskData, 0, entry.RamdiskSize);

                    using var fs = File.Create(filePath);
                    if (!skipDecomp && IsCompressed(entry.Format))
                    {
                        DecompressData(ramdiskData, entry.Format, fs);
                    }
                    else
                    {
                        fs.Write(ramdiskData, 0, ramdiskData.Length);
                    }
                }
            }
            else if (!skipDecomp && IsCompressed(bootImage.RamdiskFormat))
            {
                if (bootImage.Ramdisk != null && bootImage.Ramdisk.Length > 0)
                {
                    using var fs = File.Create(BootImage.RAMDISK_FILE);
                    DecompressData(bootImage.Ramdisk, bootImage.RamdiskFormat, fs);
                }
            }
            else
            {
                if (bootImage.Ramdisk != null && bootImage.Ramdisk.Length > 0)
                    File.WriteAllBytes(BootImage.RAMDISK_FILE, bootImage.Ramdisk);
            }

            // 导出second
            if (bootImage.Second != null && bootImage.Second.Length > 0)
                File.WriteAllBytes(BootImage.SECOND_FILE, bootImage.Second);

            // 解压并导出extra
            if (!skipDecomp && IsCompressed(bootImage.ExtraFormat))
            {
                if (bootImage.Extra != null && bootImage.Extra.Length > 0)
                {
                    using var fs = File.Create(BootImage.EXTRA_FILE);
                    DecompressData(bootImage.Extra, bootImage.ExtraFormat, fs);
                }
            }
            else
            {
                if (bootImage.Extra != null && bootImage.Extra.Length > 0)
                    File.WriteAllBytes(BootImage.EXTRA_FILE, bootImage.Extra);
            }

            // 导出recovery_dtbo
            if (bootImage.RecoveryDtbo != null && bootImage.RecoveryDtbo.Length > 0)
                File.WriteAllBytes(BootImage.RECV_DTBO_FILE, bootImage.RecoveryDtbo);

            // 导出dtb
            if (bootImage.Dtb != null && bootImage.Dtb.Length > 0)
                File.WriteAllBytes(BootImage.DTB_FILE, bootImage.Dtb);

            // 导出bootconfig
            if (bootImage.Bootconfig != null && bootImage.Bootconfig.Length > 0)
                File.WriteAllBytes(BootImage.BOOTCONFIG_FILE, bootImage.Bootconfig);

            return bootImage.IsChromeOS ? 2 : 0;
        }

        /// <summary>
        /// 判断格式是否是压缩格式
        /// </summary>
        private static bool IsCompressed(Format format)
        {
            return format switch
            {
                Format.GZIP or Format.ZOPFLI or Format.LZOP or Format.XZ or
                Format.LZMA or Format.BZIP2 or Format.LZ4 or
                Format.LZ4_LEGACY or Format.LZ4_LG => true,
                _ => false
            };
        }

        /// <summary>
        /// 解压数据
        /// </summary>
        private static void DecompressData(byte[] data, Format format, Stream outputStream)
        {
            using var inputStream = new MemoryStream(data);

            switch (format)
            {
                case Format.GZIP:
                case Format.ZOPFLI:
                    GzipUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.XZ:
                    XZUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.LZMA:
                    LzmaUtils.Decompress(inputStream, outputStream);
                    break;
                case Format.BZIP2:
                    BZip2Utils.Decompress(inputStream, outputStream);
                    break;
                case Format.LZ4:
                    // 对于LZ4，需要读取整个流然后解压
                    {
                        using var ms = new MemoryStream();
                        inputStream.CopyTo(ms);
                        byte[] compressedData = ms.ToArray();
                        byte[] decompressedData = LZ4Compressor.AutoDecompress(compressedData);
                        outputStream.Write(decompressedData, 0, decompressedData.Length);
                    }
                    break;
                case Format.LZ4_LEGACY:
                    {
                        using var ms = new MemoryStream();
                        inputStream.CopyTo(ms);
                        byte[] compressedData = ms.ToArray();
                        byte[] decompressedData = LZ4Compressor.Decompress(compressedData, LZ4Compressor.LZ4Format.Legacy);
                        outputStream.Write(decompressedData, 0, decompressedData.Length);
                    }
                    break;
                case Format.LZ4_LG:
                    {
                        using var ms = new MemoryStream();
                        inputStream.CopyTo(ms);
                        byte[] compressedData = ms.ToArray();
                        byte[] decompressedData = LZ4Compressor.Decompress(compressedData, LZ4Compressor.LZ4Format.LegacyWithSize);
                        outputStream.Write(decompressedData, 0, decompressedData.Length);
                    }
                    break;
                default:
                    // 对于未知格式，直接复制
                    inputStream.CopyTo(outputStream);
                    break;
            }
        }
    }
}