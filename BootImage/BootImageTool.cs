using Compress;

namespace BootImage
{
    /// <summary>
    /// 引导镜像工具类，提供解包、重打包、签名、验证等功能的集中入口
    /// </summary>
    public static class BootImageTool
    {
        /// <summary>
        /// 解包引导镜像
        /// </summary>
        /// <param name="imagePath">引导镜像路径</param>
        /// <param name="skipDecomp">是否跳过解压缩</param>
        /// <param name="extractHeader">是否提取头部信息</param>
        /// <returns>如果是ChromeOS镜像返回2，否则返回0</returns>
        public static int Unpack(string imagePath, bool skipDecomp = false, bool extractHeader = true)
        {
            try
            {
                Console.WriteLine($"解包引导镜像: [{imagePath}]");
                return BootImageUnpacker.Unpack(imagePath, skipDecomp, extractHeader);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"解包失败: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 重新打包引导镜像
        /// </summary>
        /// <param name="srcImg">源镜像路径</param>
        /// <param name="outImg">输出镜像路径</param>
        /// <param name="skipComp">是否跳过压缩</param>
        /// <returns>是否成功</returns>
        public static bool Repack(string srcImg, string outImg, bool skipComp = false)
        {
            try
            {
                Console.WriteLine($"重新打包引导镜像: [{srcImg}] -> [{outImg}]");
                return BootImageRepacker.Repack(srcImg, outImg, skipComp);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"重打包失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证引导镜像的签名
        /// </summary>
        /// <param name="imagePath">引导镜像路径</param>
        /// <param name="certPath">证书文件路径，如果为null则只检查是否已签名</param>
        /// <returns>如果验证通过返回true，否则返回false</returns>
        public static bool Verify(string imagePath, string certPath = null)
        {
            try
            {
                Console.WriteLine($"验证引导镜像签名: [{imagePath}]");
                bool result = BootImageSigner.Verify(imagePath, certPath);

                if (certPath == null)
                {
                    Console.WriteLine($"镜像{(result ? "已" : "未")}签名");
                }
                else
                {
                    Console.WriteLine($"使用证书 [{certPath}] 验证结果: {(result ? "通过" : "失败")}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"验证失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 对引导镜像进行签名
        /// </summary>
        /// <param name="imagePath">引导镜像路径</param>
        /// <param name="name">签名名称，通常为"/boot"</param>
        /// <param name="certPath">证书文件路径，如果为null则使用默认测试证书</param>
        /// <param name="keyPath">私钥文件路径，如果为null则使用默认测试私钥</param>
        /// <returns>如果签名成功返回true，否则返回false</returns>
        public static bool Sign(string imagePath, string name = "/boot", string certPath = null, string keyPath = null)
        {
            try
            {
                Console.WriteLine($"对引导镜像进行签名: [{imagePath}]");
                bool result = BootImageSigner.Sign(imagePath, name, certPath, keyPath);

                Console.WriteLine($"签名{(result ? "成功" : "失败")}");
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"签名失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从内核中提取DTB
        /// </summary>
        /// <param name="kernelPath">内核文件路径</param>
        /// <param name="skipDecomp">是否跳过解压缩</param>
        /// <returns>如果提取成功返回true，否则返回false</returns>
        public static bool SplitImageDtb(string kernelPath, bool skipDecomp = false)
        {
            try
            {
                Console.WriteLine($"从内核中提取DTB: [{kernelPath}]");
                bool result = BootImageSigner.SplitImageDtb(kernelPath, skipDecomp);

                Console.WriteLine($"提取{(result ? "成功" : "失败")}");
                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"提取失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 打印引导镜像信息
        /// </summary>
        /// <param name="imagePath">引导镜像路径</param>
        /// <returns>是否成功</returns>
        public static bool PrintInfo(string imagePath)
        {
            try
            {
                Console.WriteLine($"引导镜像信息: [{imagePath}]");

                using var bootImage = new BootImage(imagePath);

                // 打印头部版本
                Console.WriteLine($"HEADER_VER: [{bootImage.HeaderVersion}]");

                // 打印内核大小
                Console.WriteLine($"KERNEL_SZ: [{bootImage.KernelSize}]");

                // 打印ramdisk大小
                Console.WriteLine($"RAMDISK_SZ: [{bootImage.RamdiskSize}]");

                // 打印second大小（如果适用）
                if (bootImage.HeaderVersion < 3)
                    Console.WriteLine($"SECOND_SZ: [{bootImage.SecondSize}]");

                // 打印extra大小（如果适用）
                if (bootImage.HeaderVersion == 0)
                    Console.WriteLine($"EXTRA_SZ: [{bootImage.ExtraSize}]");

                // 打印recovery dtbo大小（如果适用）
                if (bootImage.HeaderVersion == 1 || bootImage.HeaderVersion == 2)
                    Console.WriteLine($"RECOV_DTBO_SZ: [{bootImage.RecoveryDtboSize}]");

                // 打印dtb大小（如果适用）
                if (bootImage.HeaderVersion == 2 || bootImage.HeaderVersion >= 3)
                    Console.WriteLine($"DTB_SZ: [{bootImage.DtbSize}]");

                // 打印页大小
                Console.WriteLine($"PAGESIZE: [{bootImage.PageSize}]");

                // 打印OS版本（如果有）
                if (bootImage.OsVersion != 0)
                {
                    int version = (int)(bootImage.OsVersion >> 11);
                    int patchLevel = (int)(bootImage.OsVersion & 0x7ff);

                    int a = version >> 14 & 0x7f;
                    int b = version >> 7 & 0x7f;
                    int c = version & 0x7f;
                    Console.WriteLine($"OS_VERSION: [{a}.{b}.{c}]");

                    int y = (patchLevel >> 4) + 2000;
                    int m = patchLevel & 0xf;
                    Console.WriteLine($"OS_PATCH_LEVEL: [{y}-{m:D2}]");
                }

                // 打印名称（如果有）
                if (!string.IsNullOrEmpty(bootImage.Name))
                    Console.WriteLine($"NAME: [{bootImage.Name}]");

                // 打印命令行
                Console.WriteLine($"CMDLINE: [{bootImage.Cmdline}{bootImage.ExtraCmdline}]");

                // 打印内核格式
                Console.WriteLine($"KERNEL_FMT: [{FormatUtils.FormatToName(bootImage.KernelFormat)}]");

                // 打印ramdisk格式
                Console.WriteLine($"RAMDISK_FMT: [{FormatUtils.FormatToName(bootImage.RamdiskFormat)}]");

                // 打印extra格式（如果有）
                if (bootImage.Extra != null && bootImage.Extra.Length > 0)
                    Console.WriteLine($"EXTRA_FMT: [{FormatUtils.FormatToName(bootImage.ExtraFormat)}]");

                // 打印标志信息
                if (bootImage.IsChromeOS)
                    Console.WriteLine("CHROMEOS");

                if (bootImage.IsDhtb)
                    Console.WriteLine("DHTB_HDR");

                if (bootImage.IsSeandroid)
                    Console.WriteLine("SAMSUNG_SEANDROID");

                if (bootImage.IsLgBump)
                    Console.WriteLine("LG_BUMP_IMAGE");

                if (bootImage.IsAvb1Signed)
                    Console.WriteLine("AVB1_SIGNED");

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"获取信息失败: {ex.Message}");
                return false;
            }
        }
    }
}