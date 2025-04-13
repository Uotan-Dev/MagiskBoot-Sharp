using Compress;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BootImage
{
    /// <summary>
    /// 引导镜像重打包器，用于将解包的组件重新打包成引导镜像
    /// </summary>
    public static class BootImageRepacker
    {
        /// <summary>
        /// 对齐值
        /// </summary>
        private static int AlignTo(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// 写入零填充
        /// </summary>
        private static void WriteZeros(FileStream fs, int count)
        {
            byte[] zeros = new byte[count];
            fs.Write(zeros, 0, zeros.Length);
        }

        /// <summary>
        /// 填充对齐
        /// </summary>
        private static void PadAlignment(FileStream fs, int startOffset, int pageSize)
        {
            int currentPos = (int)fs.Position;
            int padding = AlignTo(currentPos - startOffset, pageSize) - (currentPos - startOffset);
            if (padding > 0)
                WriteZeros(fs, padding);
        }

        /// <summary>
        /// 从文件中恢复数据到输出流
        /// </summary>
        private static int RestoreFile(FileStream outputStream, string fileName)
        {
            if (!File.Exists(fileName))
                return 0;

            byte[] data = File.ReadAllBytes(fileName);
            outputStream.Write(data, 0, data.Length);
            return data.Length;
        }

        /// <summary>
        /// 从内存缓冲区压缩数据到输出流
        /// </summary>
        private static int CompressData(FileStream outputStream, byte[] data, Format format)
        {
            long startPos = outputStream.Position;
            
            using var inputStream = new MemoryStream(data);
            CompressionManager.Compress(inputStream, outputStream, format);
            
            return (int)(outputStream.Position - startPos);
        }

        /// <summary>
        /// 更新校验和
        /// </summary>
        private static void UpdateChecksum(byte[] headerData, bool useSHA256, byte[] kernelData, byte[] ramdiskData, 
            byte[] secondData, byte[] extraData, byte[] recoveryDtboData, byte[] dtbData)
        {
            using HashAlgorithm hasher = useSHA256 ? SHA256.Create() : SHA1.Create();
            
            // 获取ID字段在头部中的偏移
            int idOffset = 576; // 基于bootimg.hpp中的定义

            // 更新校验和
            if (kernelData != null && kernelData.Length > 0)
            {
                hasher.TransformBlock(kernelData, 0, kernelData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(kernelData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            if (ramdiskData != null && ramdiskData.Length > 0)
            {
                hasher.TransformBlock(ramdiskData, 0, ramdiskData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(ramdiskData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            if (secondData != null && secondData.Length > 0)
            {
                hasher.TransformBlock(secondData, 0, secondData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(secondData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            if (extraData != null && extraData.Length > 0)
            {
                hasher.TransformBlock(extraData, 0, extraData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(extraData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            if (recoveryDtboData != null && recoveryDtboData.Length > 0)
            {
                hasher.TransformBlock(recoveryDtboData, 0, recoveryDtboData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(recoveryDtboData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            if (dtbData != null && dtbData.Length > 0)
            {
                hasher.TransformBlock(dtbData, 0, dtbData.Length, null, 0);
                byte[] size = BitConverter.GetBytes(dtbData.Length);
                hasher.TransformBlock(size, 0, size.Length, null, 0);
            }
            
            hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            
            // 清空ID字段
            for (int i = 0; i < 32; i++)
                headerData[idOffset + i] = 0;
            
            // 写入新的校验和
            byte[] hash = hasher.Hash;
            Buffer.BlockCopy(hash, 0, headerData, idOffset, hash.Length);
        }

        /// <summary>
        /// 重打包引导镜像
        /// </summary>
        /// <param name="srcImg">源镜像路径</param>
        /// <param name="outImg">输出镜像路径</param>
        /// <param name="skipComp">是否跳过压缩</param>
        /// <returns>是否成功</returns>
        public static bool Repack(string srcImg, string outImg, bool skipComp = false)
        {
            Console.WriteLine($"Repack to boot image: [{outImg}]");

            using var bootImage = new BootImage(srcImg);
            
            // 用于记录各部分的偏移
            int headerOffset = 0;
            int kernelOffset = 0;
            int ramdiskOffset = 0;
            int secondOffset = 0;
            int extraOffset = 0;
            int dtbOffset = 0;
            int totalOffset = 0;
            int vbmetaOffset = 0;

            // 创建新的输出文件
            using var fs = new FileStream(outImg, FileMode.Create);

            // 写入特殊头部（如果有的话）
            if (bootImage.IsDhtb)
            {
                // 预留DHTB头部空间，稍后填充
                WriteZeros(fs, 512);
            }
            else if (bootImage.Flags[6]) // BLOB_FLAG
            {
                // 复制BLOB头部（如果实际需要，需要更详细的处理）
                byte[] blobHeader = new byte[512];
                Buffer.BlockCopy(bootImage._fileData, 0, blobHeader, 0, 512);
                fs.Write(blobHeader, 0, 512);
            }

            // 写入原始头部空间，稍后会更新
            headerOffset = (int)fs.Position;
            byte[] headerData = new byte[AlignTo(bootImage.HeaderVersion <= 2 ? 2048 : 4096, (int)bootImage.PageSize)];
            fs.Write(headerData, 0, headerData.Length);

            // 加载头部配置（如果存在）
            Dictionary<string, string> headerProps = new Dictionary<string, string>();
            if (File.Exists(BootImage.HEADER_FILE))
            {
                foreach (string line in File.ReadAllLines(BootImage.HEADER_FILE))
                {
                    int equalPos = line.IndexOf('=');
                    if (equalPos > 0)
                    {
                        string key = line.Substring(0, equalPos).Trim();
                        string value = line.Substring(equalPos + 1).Trim();
                        headerProps[key] = value;
                    }
                }
            }

            // 处理内核
            kernelOffset = (int)fs.Position;
            
            // 如果存在KERNEL_FILE，读取并处理
            if (File.Exists(BootImage.KERNEL_FILE))
            {
                byte[] kernelData = File.ReadAllBytes(BootImage.KERNEL_FILE);
                
                // 如果需要压缩且输入不是压缩格式
                if (!skipComp && !FormatUtils.IsCompressedFormat(FormatUtils.CheckFormat(kernelData,kernelData.Length)) && 
                    FormatUtils.IsCompressedFormat(bootImage.KernelFormat))
                {
                    int size = CompressData(fs, kernelData, bootImage.KernelFormat);
                    
                    // 更新头部信息
                    BitConverter.GetBytes((uint)size).CopyTo(headerData, 8);
                }
                else
                {
                    // 直接写入
                    fs.Write(kernelData, 0, kernelData.Length);
                    
                    // 更新头部信息
                    BitConverter.GetBytes((uint)kernelData.Length).CopyTo(headerData, 8);
                }
            }
            else if (bootImage.Kernel != null && bootImage.Kernel.Length > 0)
            {
                // 如果没有KERNEL_FILE但有原始内核数据，则使用原始数据
                fs.Write(bootImage.Kernel, 0, bootImage.Kernel.Length);
                
                // 更新头部信息
                BitConverter.GetBytes((uint)bootImage.Kernel.Length).CopyTo(headerData, 8);
            }

            // 写入内核DTB（如果存在）
            if (File.Exists(BootImage.KER_DTB_FILE))
            {
                byte[] kernelDtbData = File.ReadAllBytes(BootImage.KER_DTB_FILE);
                fs.Write(kernelDtbData, 0, kernelDtbData.Length);
                
                // 更新内核大小，加上DTB大小
                uint kernelSize = BitConverter.ToUInt32(headerData, 8);
                kernelSize += (uint)kernelDtbData.Length;
                BitConverter.GetBytes(kernelSize).CopyTo(headerData, 8);
            }
            
            // 页面对齐
            PadAlignment(fs, headerOffset, (int)bootImage.PageSize);

            // 处理ramdisk
            ramdiskOffset = (int)fs.Position;

            if (bootImage.HasVendorRamdiskTable)
            {
                // 处理多个vendor ramdisks（如果项目实现了vendor ramdisk表的解析处理）
                if (Directory.Exists(BootImage.VND_RAMDISK_DIR))
                {
                    // 详细实现将根据项目中vendor ramdisk的处理逻辑来完善
                }
            }
            else if (File.Exists(BootImage.RAMDISK_FILE))
            {
                byte[] ramdiskData = File.ReadAllBytes(BootImage.RAMDISK_FILE);
                
                // 如果需要压缩且输入不是压缩格式
                if (!skipComp && !FormatUtils.IsCompressedFormat(FormatUtils.CheckFormat(ramdiskData,ramdiskData.Length)) && 
                    FormatUtils.IsCompressedFormat(bootImage.RamdiskFormat))
                {
                    int size = CompressData(fs, ramdiskData, bootImage.RamdiskFormat);
                    
                    // 更新头部信息
                    BitConverter.GetBytes((uint)size).CopyTo(headerData, 16);
                }
                else
                {
                    // 直接写入
                    fs.Write(ramdiskData, 0, ramdiskData.Length);
                    
                    // 更新头部信息
                    BitConverter.GetBytes((uint)ramdiskData.Length).CopyTo(headerData, 16);
                }
                
                // 页面对齐
                PadAlignment(fs, headerOffset, (int)bootImage.PageSize);
            }

            // 处理second
            secondOffset = (int)fs.Position;
            if (File.Exists(BootImage.SECOND_FILE))
            {
                int size = RestoreFile(fs, BootImage.SECOND_FILE);
                
                // 更新头部信息
                BitConverter.GetBytes((uint)size).CopyTo(headerData, 24);
                
                // 页面对齐
                PadAlignment(fs, headerOffset, (int)bootImage.PageSize);
            }

            // 处理extra
            extraOffset = (int)fs.Position;
            if (File.Exists(BootImage.EXTRA_FILE))
            {
                byte[] extraData = File.ReadAllBytes(BootImage.EXTRA_FILE);
                
                // 如果需要压缩且输入不是压缩格式
                if (!skipComp && !FormatUtils.IsCompressedFormat(FormatUtils.CheckFormat(extraData,extraData.Length)) && 
                    FormatUtils.IsCompressedFormat(bootImage.ExtraFormat))
                {
                    int size = CompressData(fs, extraData, bootImage.ExtraFormat);
                    
                    // 更新头部信息（如果适用）
                    if (bootImage.HeaderVersion == 0)
                    {
                        BitConverter.GetBytes((uint)size).CopyTo(headerData, 48);
                    }
                }
                else
                {
                    // 直接写入
                    fs.Write(extraData, 0, extraData.Length);
                    
                    // 更新头部信息（如果适用）
                    if (bootImage.HeaderVersion == 0)
                    {
                        BitConverter.GetBytes((uint)extraData.Length).CopyTo(headerData, 48);
                    }
                }
                
                // 页面对齐
                PadAlignment(fs, headerOffset, (int)bootImage.PageSize);
            }

            // 处理recovery_dtbo
            if (File.Exists(BootImage.RECV_DTBO_FILE))
            {
                int dtboOffset = (int)fs.Position;
                int size = RestoreFile(fs, BootImage.RECV_DTBO_FILE);
                
                // 更新头部信息（如果适用）
                if (bootImage.HeaderVersion == 1 || bootImage.HeaderVersion == 2)
                {
                    BitConverter.GetBytes((uint)size).CopyTo(headerData, 1632);
                    BitConverter.GetBytes(dtboOffset).CopyTo(headerData, 1624);
                }
                
                // 页面对齐
                PadAlignment(fs, headerOffset, (int)bootImage.PageSize);
            }

            // 处理dtb
            dtbOffset = (int)fs.Position;
            if (File.Exists(BootImage.DTB_FILE))
            {
                int size = RestoreFile(fs, BootImage.DTB_FILE);
                
                // 更新头部信息（如果适用）
                if (bootImage.HeaderVersion == 2)
                {
                    BitConverter.GetBytes((uint)size).CopyTo(headerData, 1648);
                }
                
                // 页面对齐
                PadAlignment(fs, headerOffset, (int)bootImage.PageSize);
            }

            // 处理特殊标志
            if (bootImage.IsSeandroid)
            {
                fs.Write(Encoding.ASCII.GetBytes("SEANDROIDENFORCE"), 0, 16);
                
                if (bootImage.IsDhtb)
                {
                    fs.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
                }
            }
            else if (bootImage.IsLgBump)
            {
                fs.Write(Encoding.ASCII.GetBytes("LGANDROIDBOOT"), 0, 16);
            }

            totalOffset = (int)fs.Position;

            // 更新头部信息
            if (headerProps.ContainsKey("cmdline"))
            {
                byte[] cmdlineBytes = Encoding.ASCII.GetBytes(headerProps["cmdline"]);
                int maxLen = Math.Min(cmdlineBytes.Length, 512 + 1024);
                Buffer.BlockCopy(cmdlineBytes, 0, headerData, 64, Math.Min(maxLen, 512));
                
                if (maxLen > 512)
                    Buffer.BlockCopy(cmdlineBytes, 512, headerData, 576, maxLen - 512);
            }
            
            if (headerProps.ContainsKey("name"))
            {
                byte[] nameBytes = Encoding.ASCII.GetBytes(headerProps["name"]);
                Buffer.BlockCopy(nameBytes, 0, headerData, 32, Math.Min(nameBytes.Length, 16));
            }
            
            if (headerProps.ContainsKey("os_version"))
            {
                string[] version = headerProps["os_version"].Split('.');
                if (version.Length == 3)
                {
                    int a = int.Parse(version[0]);
                    int b = int.Parse(version[1]);
                    int c = int.Parse(version[2]);
                    
                    // 读取当前os_version获取patch_level
                    uint currentOsVersion = BitConverter.ToUInt32(headerData, 44);
                    uint patchLevel = currentOsVersion & 0x7ffu;
                    
                    // 构建新的os_version
                    uint newOsVersion = (((uint)a << 14) | ((uint)b << 7) | (uint)c) << 11;
                    newOsVersion |= patchLevel;
                    
                    BitConverter.GetBytes(newOsVersion).CopyTo(headerData, 44);
                }
            }
            
            if (headerProps.ContainsKey("os_patch_level"))
            {
                string[] patchLevel = headerProps["os_patch_level"].Split('-');
                if (patchLevel.Length == 2)
                {
                    int y = int.Parse(patchLevel[0]) - 2000;
                    int m = int.Parse(patchLevel[1]);
                    
                    // 读取当前os_version
                    uint currentOsVersion = BitConverter.ToUInt32(headerData, 44);
                    uint osVer = currentOsVersion >> 11;
                    
                    // 构建新的os_version
                    uint newOsVersion = (osVer << 11) | ((uint)y << 4) | (uint)m;
                    
                    BitConverter.GetBytes(newOsVersion).CopyTo(headerData, 44);
                }
            }

            // 更新校验和
            UpdateChecksum(
                headerData,
                bootImage.Flags[11], // SHA256_FLAG
                File.Exists(BootImage.KERNEL_FILE) ? File.ReadAllBytes(BootImage.KERNEL_FILE) : null,
                File.Exists(BootImage.RAMDISK_FILE) ? File.ReadAllBytes(BootImage.RAMDISK_FILE) : null,
                File.Exists(BootImage.SECOND_FILE) ? File.ReadAllBytes(BootImage.SECOND_FILE) : null,
                File.Exists(BootImage.EXTRA_FILE) ? File.ReadAllBytes(BootImage.EXTRA_FILE) : null,
                File.Exists(BootImage.RECV_DTBO_FILE) ? File.ReadAllBytes(BootImage.RECV_DTBO_FILE) : null,
                File.Exists(BootImage.DTB_FILE) ? File.ReadAllBytes(BootImage.DTB_FILE) : null
            );

            // 将更新后的头部写回文件
            fs.Position = headerOffset;
            fs.Write(headerData, 0, headerData.Length);
            fs.Position = fs.Length;

            // 如果是DHTB头部，需要额外处理
            if (bootImage.IsDhtb)
            {
                fs.Position = 0;
                DhtbHeader dhtbHeader = new DhtbHeader
                {
                    Magic = Encoding.ASCII.GetBytes("DHTB"),
                    Size = (uint)totalOffset - 512
                };
                
                // 写入魔数
                fs.Write(dhtbHeader.Magic, 0, dhtbHeader.Magic.Length);
                
                // 留空校验和区域
                byte[] checksumData = new byte[40];
                fs.Write(checksumData, 0, checksumData.Length);
                
                // 写入大小
                byte[] sizeBytes = BitConverter.GetBytes(dhtbHeader.Size);
                fs.Write(sizeBytes, 0, sizeBytes.Length);
                
                // 计算校验和
                fs.Position = 512;
                byte[] fileData = new byte[dhtbHeader.Size];
                fs.Read(fileData, 0, fileData.Length);
                
                using var sha256 = SHA256.Create();
                byte[] checksum = sha256.ComputeHash(fileData);
                
                // 写入校验和
                fs.Position = 8;
                fs.Write(checksum, 0, checksum.Length);
            }

            return true;
        }
    }
}