using Compress;
using Sign;

namespace BootImage
{
    /// <summary>
    /// 引导镜像签名和验证工具
    /// </summary>
    public static class BootImageSigner
    {
        /// <summary>
        /// 验证引导镜像的签名
        /// </summary>
        /// <param name="imagePath">引导镜像文件路径</param>
        /// <param name="certPath">证书文件路径，如果为null则只检查是否已签名</param>
        /// <returns>如果镜像已签名或验证通过则返回true，否则返回false</returns>
        public static bool Verify(string imagePath, string certPath = null)
        {
            using var bootImage = new BootImage(imagePath);

            // 如果未提供证书，则只检查镜像是否已签名
            if (certPath == null)
            {
                // 在BootImage类中，AVB1_SIGNED_FLAG索引表示镜像是否已签名
                return bootImage.Flags[13]; // AVB1_SIGNED_FLAG
            }
            else
            {
                // 使用Sign库中的BootSignatureUtils.VerifyBootImage方法
                var img = new Sign.BootImage(File.ReadAllBytes(imagePath), GetPayloadSize(bootImage));
                return BootSignatureUtils.VerifyBootImage(img, certPath);
            }
        }

        /// <summary>
        /// 对引导镜像进行签名
        /// </summary>
        /// <param name="imagePath">引导镜像文件路径</param>
        /// <param name="name">签名名称，通常为"/boot"</param>
        /// <param name="certPath">证书文件路径，如果为null则使用默认测试证书</param>
        /// <param name="keyPath">私钥文件路径，如果为null则使用默认测试私钥</param>
        /// <returns>签名是否成功</returns>
        public static bool Sign(string imagePath, string name = "/boot", string certPath = null, string keyPath = null)
        {
            try
            {
                using var bootImage = new BootImage(imagePath);
                Console.WriteLine($"为镜像 {imagePath} 签名，名称={name}");

                // 获取要签名的负载部分
                byte[] fileData = File.ReadAllBytes(imagePath);
                int payloadSize = GetPayloadSize(bootImage);
                byte[] payload = new byte[payloadSize];
                Array.Copy(fileData, payload, payloadSize);

                // 使用Sign库中的BootSignatureUtils.SignBootImage方法生成签名数据
                byte[] signature = BootSignatureUtils.SignBootImage(payload, name, certPath, keyPath);

                if (signature == null || signature.Length == 0)
                {
                    Console.WriteLine("生成签名数据失败");
                    return false;
                }

                // 打开文件进行修改
                using var fs = new FileStream(imagePath, FileMode.Open, FileAccess.ReadWrite);

                // 计算EOF位置：原始尾部数据开始的位置
                long eof = bootImage._fileData.Length - (bootImage.Tail?.Length ?? 0);

                // 在文件末尾添加签名块
                fs.Position = eof;

                // 写入签名数据
                fs.Write(signature, 0, signature.Length);

                // 如果需要，用零填充剩余空间
                long currentPosition = fs.Position;
                if (currentPosition < bootImage._fileData.Length)
                {
                    byte[] padding = new byte[bootImage._fileData.Length - currentPosition];
                    fs.Write(padding, 0, padding.Length);
                }

                Console.WriteLine("签名成功");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"签名失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从内核提取DTB
        /// </summary>
        /// <param name="kernelPath">内核文件路径</param>
        /// <param name="skipDecomp">是否跳过解压缩</param>
        /// <returns>如果提取成功返回true，否则返回false</returns>
        public static bool SplitImageDtb(string kernelPath, bool skipDecomp = false)
        {
            try
            {
                // 读取内核文件
                byte[] kernel = File.ReadAllBytes(kernelPath);

                // 查找DTB偏移
                int dtbOffset = FindDtbOffset(kernel);
                if (dtbOffset > 0)
                {
                    // 检测内核格式
                    Format format = FormatUtils.CheckFormat(kernel, kernel.Length);

                    if (!skipDecomp && FormatUtils.IsCompressedFormat(format))
                    {
                        // 解压内核部分
                        using var fs = File.Create(BootImage.KERNEL_FILE);
                        using var ms = new MemoryStream(kernel, 0, dtbOffset);
                        CompressionManager.AutoDecompress(ms, fs);
                    }
                    else
                    {
                        // 直接写入内核部分
                        File.WriteAllBytes(BootImage.KERNEL_FILE, kernel.AsSpan(0, dtbOffset).ToArray());
                    }

                    // 写入DTB部分
                    File.WriteAllBytes(BootImage.KER_DTB_FILE, kernel.AsSpan(dtbOffset).ToArray());

                    Console.WriteLine($"成功从 {kernelPath} 提取DTB");
                    return true;
                }
                else
                {
                    Console.WriteLine($"在 {kernelPath} 中未找到DTB");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"提取DTB失败：{ex.Message}");
                return false;
            }
        }

        #region 私有方法
        /// <summary>
        /// 获取负载部分的大小
        /// </summary>
        private static int GetPayloadSize(BootImage bootImage)
        {
            // 计算头部空间
            int headerSize = bootImage.HeaderVersion switch
            {
                0 => 2048,
                1 => 2048 + 16 + 4,
                2 => 2048 + 16 + 4 + 4 + 4,
                _ => bootImage.HeaderVersion <= 2 ? 2048 : 4096
            };

            int headerSpace = AlignTo(headerSize, (int)bootImage.PageSize);

            // 计算内核空间
            int kernelSpace = AlignTo(bootImage.Kernel?.Length ?? 0, (int)bootImage.PageSize);

            // 计算ramdisk空间
            int ramdiskSpace = AlignTo(bootImage.Ramdisk?.Length ?? 0, (int)bootImage.PageSize);

            // 计算second空间
            int secondSpace = AlignTo(bootImage.Second?.Length ?? 0, (int)bootImage.PageSize);

            // 计算extra空间
            int extraSpace = AlignTo(bootImage.Extra?.Length ?? 0, (int)bootImage.PageSize);

            // 计算dtb空间
            int dtbSpace = AlignTo(bootImage.Dtb?.Length ?? 0, (int)bootImage.PageSize);

            // 负载总大小
            return headerSpace + kernelSpace + ramdiskSpace + secondSpace + extraSpace + dtbSpace;
        }

        /// <summary>
        /// 对齐值
        /// </summary>
        private static int AlignTo(int value, int alignment)
        {
            return (value + alignment - 1) & ~(alignment - 1);
        }

        /// <summary>
        /// 查找DTB在内核中的偏移
        /// </summary>
        private static int FindDtbOffset(byte[] kernel)
        {
            // DTB魔数，小端序FDT魔数 (0xD00DFEED)
            byte[] dtbMagic = new byte[] { 0xD0, 0x0D, 0xFE, 0xED };

            // 从头开始搜索DTB魔数
            for (int i = 0; i <= kernel.Length - dtbMagic.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < dtbMagic.Length; j++)
                {
                    if (kernel[i + j] != dtbMagic[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    // 找到魔数后，还需要进行额外验证以确保这是真正的DTB头部
                    if (i + 8 <= kernel.Length)
                    {
                        // 读取totalsize
                        uint totalsize = BitConverter.ToUInt32(kernel, i + 4);

                        // 检查totalsize是否超出内核大小
                        if (totalsize > kernel.Length - i)
                            continue;

                        // 读取off_dt_struct
                        uint offDtStruct = BitConverter.ToUInt32(kernel, i + 8);

                        // 检查off_dt_struct是否超出内核大小
                        if (offDtStruct > kernel.Length - i)
                            continue;

                        // 检查第一个节点的tag是否为FDT_BEGIN_NODE (1)
                        if (i + offDtStruct + 4 <= kernel.Length)
                        {
                            uint nodeTag = BitConverter.ToUInt32(kernel, i + (int)offDtStruct);
                            if (nodeTag == 1)
                                return i; // 找到有效的DTB
                        }
                    }
                }
            }

            return -1; // 未找到DTB
        }
        #endregion
    }
}