using Compress;
using System.Runtime.InteropServices;
using System.Text;

namespace BootImage
{
    #region 特殊头部结构体定义
    /// <summary>
    /// MTK头部结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MtkHeader
    {
        public uint Magic;            // MTK魔数
        public uint Size;             // 内容大小
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Name;           // 头部类型名称
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 472)]
        public byte[] Padding;        // 填充至512字节

        public string NameString => Encoding.ASCII.GetString(Name).TrimEnd('\0');
    }

    /// <summary>
    /// DHTB头部结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DhtbHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Magic;          // DHTB魔数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] Checksum;       // 负载SHA256，整个镜像+SEANDROIDENFORCE+0xFFFFFFFF
        public uint Size;             // 负载大小，整个镜像+SEANDROIDENFORCE+0xFFFFFFFF
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 460)]
        public byte[] Padding;        // 填充至512字节
    }

    /// <summary>
    /// BLOB头部结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlobHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] SecureMagic;    // "-SIGNED-BY-SIGNBLOB-"
        public uint DataLen;          // 0x00000000
        public uint Signature;        // 0x00000000
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Magic;          // "MSM-RADIO-UPDATE"
        public uint HeaderVersion;    // 0x00010000
        public uint HeaderSize;       // 头部大小
        public uint PartOffset;       // 与大小相同
        public uint NumParts;         // 分区数量
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] Unknown;        // 全部为0x00000000
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Name;           // 分区名称
        public uint Offset;           // 分区起始偏移
        public uint Size;             // 数据大小
        public uint Version;          // 0x00000001
    }

    /// <summary>
    /// zImage头部结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ZImageHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public uint[] Code;
        public uint Magic;            // zImage魔数
        public uint Start;            // zImage绝对加载/运行地址
        public uint End;              // zImage结束地址
        public uint Endian;           // 字节序标志
        // 可能还有更多字段，但我们不关心
    }

    /// <summary>
    /// AVB Footer结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AvbFooter
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Magic;          // AVB Footer魔数
        public uint VersionMajor;
        public uint VersionMinor;
        public ulong OriginalImageSize;
        public ulong VbmetaOffset;
        public ulong VbmetaSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
        public byte[] Reserved;
    }

    /// <summary>
    /// AVB VBMeta镜像头部
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AvbVBMetaImageHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Magic;
        public uint RequiredLibavbVersionMajor;
        public uint RequiredLibavbVersionMinor;
        public ulong AuthenticationDataBlockSize;
        public ulong AuxiliaryDataBlockSize;
        public uint AlgorithmType;
        public ulong HashOffset;
        public ulong HashSize;
        public ulong SignatureOffset;
        public ulong SignatureSize;
        public ulong PublicKeyOffset;
        public ulong PublicKeySize;
        public ulong PublicKeyMetadataOffset;
        public ulong PublicKeyMetadataSize;
        public ulong DescriptorsOffset;
        public ulong DescriptorsSize;
        public ulong RollbackIndex;
        public uint Flags;
        public uint RollbackIndexLocation;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] ReleaseString;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
        public byte[] Reserved;
    }
    #endregion

    #region 引导镜像头部结构体定义
    /// <summary>
    /// Android引导镜像通用头部V0部分
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV0Common
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Magic;

        public uint KernelSize;        // 字节大小
        public uint KernelAddr;        // 物理加载地址

        public uint RamdiskSize;       // 字节大小
        public uint RamdiskAddr;       // 物理加载地址

        public uint SecondSize;        // 字节大小
        public uint SecondAddr;        // 物理加载地址
    }

    /// <summary>
    /// Android引导镜像头部V0
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV0 : IBootImgHdr
    {
        public BootImgHdrV0Common Common;

        public uint TagsAddr;          // 内核标签物理地址

        // 在AOSP头部中，此字段用于页面大小
        // 对于三星PXA头部，此字段的用途未知，但其值不真实，不可能作为页面大小
        // 我们用这个事实来确定这是AOSP还是PXA头部
        public uint PageSize;          // 我们假设的闪存页面大小

        // 在v1头部中，此字段用于头部版本
        // 但在某些设备上，如三星，此字段用于存储DTB
        // 我们根据其值区分对待此字段
        public uint HeaderVersion;     // 头部版本

        // 操作系统版本和安全补丁级别
        // 对于版本"A.B.C"和补丁级别"Y-M-D":
        // (每个A，B，C各7位; Y-2000有7位，M有4位)
        // os_version = A[31:25] B[24:18] C[17:11] (Y-2000)[10:4] M[3:0]
        public uint OsVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Name;            // 以null结尾的产品名称

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] Cmdline;         // 命令行

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Id;              // 时间戳/校验和/sha1等

        // 补充命令行数据，保留在此处以保持与旧版本mkbootimg的二进制兼容性
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] ExtraCmdline;

        public int GetHeaderVersion() => (int)HeaderVersion;
        public int GetPageSize() => (int)PageSize;
    }

    /// <summary>
    /// Android引导镜像头部V1
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV1 : IBootImgHdr
    {
        public BootImgHdrV0 V0;

        public uint RecoveryDtboSize;     // recovery DTBO/ACPIO镜像的字节大小
        public ulong RecoveryDtboOffset;  // 引导镜像中recovery dtbo/acpio的偏移量
        public uint HeaderSize;

        public int GetHeaderVersion() => V0.GetHeaderVersion();
        public int GetPageSize() => V0.GetPageSize();
    }

    /// <summary>
    /// Android引导镜像头部V2
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV2 : IBootImgHdr
    {
        public BootImgHdrV1 V1;

        public uint DtbSize;           // DTB镜像的字节大小
        public ulong DtbAddr;          // DTB镜像的物理加载地址

        public int GetHeaderVersion() => V1.GetHeaderVersion();
        public int GetPageSize() => V1.GetPageSize();
    }

    /// <summary>
    /// 特殊的三星PXA头部
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrPxa : IBootImgHdr
    {
        public BootImgHdrV0Common Common;

        public uint ExtraSize;         // 额外blob的字节大小
        public uint Unknown;
        public uint TagsAddr;          // 内核标签物理地址
        public uint PageSize;          // 我们假设的闪存页面大小

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] Name;            // 以null结尾的产品名称

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] Cmdline;         // 命令行

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Id;              // 时间戳/校验和/sha1等

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] ExtraCmdline;    // 额外命令行

        public int GetHeaderVersion() => 0; // PXA头部没有版本，返回0
        public int GetPageSize() => (int)PageSize;
    }

    /// <summary>
    /// Android引导镜像头部V3
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV3 : IBootImgHdr
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Magic;

        public uint KernelSize;        // 字节大小
        public uint RamdiskSize;       // 字节大小
        public uint OsVersion;
        public uint HeaderSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] Reserved;

        public uint HeaderVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1536)] // 512 + 1024
        public byte[] Cmdline;         // 命令行 (包含额外命令行)

        public int GetHeaderVersion() => (int)HeaderVersion;
        public int GetPageSize() => 4096; // V3固定页面大小为4096
    }

    /// <summary>
    /// Android Vendor引导镜像头部V3
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrVndV3 : IBootImgHdr
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Magic;           // 必须是VENDOR_BOOT_MAGIC

        public uint HeaderVersion;     // Vendor引导镜像头部版本
        public uint PageSize;          // 我们假设的闪存页面大小
        public uint KernelAddr;        // 物理加载地址
        public uint RamdiskAddr;       // 物理加载地址
        public uint RamdiskSize;       // 字节大小

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]
        public byte[] Cmdline;         // 命令行

        public uint TagsAddr;          // 内核标签物理地址

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] Name;            // 以null结尾的产品名称

        public uint HeaderSize;
        public uint DtbSize;           // DTB镜像的字节大小
        public ulong DtbAddr;          // DTB镜像的物理加载地址

        public int GetHeaderVersion() => (int)HeaderVersion;
        public int GetPageSize() => (int)PageSize;
    }

    /// <summary>
    /// Android引导镜像头部V4
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrV4 : IBootImgHdr
    {
        public BootImgHdrV3 V3;

        public uint SignatureSize;     // 字节大小

        public int GetHeaderVersion() => V3.GetHeaderVersion();
        public int GetPageSize() => V3.GetPageSize();
    }

    /// <summary>
    /// Android Vendor引导镜像头部V4
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BootImgHdrVndV4 : IBootImgHdr
    {
        public BootImgHdrVndV3 V3;

        public uint VendorRamdiskTableSize;      // Vendor ramdisk表的字节大小
        public uint VendorRamdiskTableEntryNum;  // Vendor ramdisk表中的条目数
        public uint VendorRamdiskTableEntrySize; // Vendor ramdisk表条目的字节大小
        public uint BootconfigSize;              // Bootconfig部分的字节大小

        public int GetHeaderVersion() => V3.GetHeaderVersion();
        public int GetPageSize() => V3.GetPageSize();
    }

    /// <summary>
    /// Vendor ramdisk表条目V4
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VendorRamdiskTableEntryV4
    {
        public uint RamdiskSize;       // ramdisk镜像的字节大小
        public uint RamdiskOffset;     // Vendor ramdisk部分中ramdisk镜像的偏移量
        public uint RamdiskType;       // ramdisk的类型

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] RamdiskName;     // 以null结尾的ramdisk名称

        // 描述此ramdisk要加载的主板、soc或平台的硬件标识符
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public uint[] BoardId;
    }
    #endregion

    /// <summary>
    /// 引导镜像头部接口
    /// </summary>
    public interface IBootImgHdr
    {
        int GetHeaderVersion();
        int GetPageSize();
    }

    /// <summary>
    /// 表示Android引导镜像文件
    /// </summary>
    public class BootImage : IDisposable
    {
        #region 常量定义
        // 魔数常量
        private const string BOOT_MAGIC = "ANDROID!";
        private const string VENDOR_BOOT_MAGIC = "VNDRBOOT";
        private const string CHROMEOS_MAGIC = "CHROMEOS";
        private const string DHTB_MAGIC = "DHTB";
        private const string SEANDROID_MAGIC = "SEANDROIDENFORCE";
        private const string LG_BUMP_MAGIC = "LGANDROIDBOOT";
        private const string AVB_MAGIC = "AVB0";
        private const string AVB_FOOTER_MAGIC = "AVBf";
        private const string DTB_MAGIC = "\xD0\x0D\xFE\xED"; // 小端序FD_DTB魔数

        // 文件名常量
        public const string HEADER_FILE = "header";
        public const string KERNEL_FILE = "kernel";
        public const string KER_DTB_FILE = "kernel_dtb";
        public const string RAMDISK_FILE = "ramdisk";
        public const string SECOND_FILE = "second";
        public const string EXTRA_FILE = "extra";
        public const string RECV_DTBO_FILE = "recovery_dtbo";
        public const string DTB_FILE = "dtb";
        public const string BOOTCONFIG_FILE = "bootconfig";
        public const string VND_RAMDISK_DIR = "vendor_ramdisk";

        // 标志位索引
        private const int CHROMEOS_FLAG = 0;
        private const int DHTB_FLAG = 1;
        private const int SEANDROID_FLAG = 2;
        private const int LG_BUMP_FLAG = 3;
        private const int MTK_KERNEL = 4;
        private const int MTK_RAMDISK = 5;
        private const int BLOB_FLAG = 6;
        private const int NOOKHD_FLAG = 7;
        private const int ACCLAIM_FLAG = 8;
        private const int AMONET_FLAG = 9;
        private const int ZIMAGE_KERNEL = 10;
        private const int SHA256_FLAG = 11;
        private const int AVB_FLAG = 12;
        private const int AVB1_SIGNED_FLAG = 13;
        #endregion

        #region 属性

        // 文件映射
        public byte[] _fileData; // 修改为public以便签名工具访问
        private string _filePath;

        // 标志位
        public bool[] Flags { get; } = new bool[32]; // 使用数组存储标志位

        // 组件数据
        public byte[] Kernel { get; private set; }
        public byte[] KernelDtb { get; private set; }
        public byte[] Ramdisk { get; private set; }
        public byte[] Second { get; private set; }
        public byte[] Extra { get; private set; }
        public byte[] RecoveryDtbo { get; private set; }
        public byte[] Dtb { get; private set; }
        public byte[] VendorRamdiskTable { get; private set; }
        public byte[] Bootconfig { get; private set; }
        public byte[] Signature { get; private set; }
        public byte[] Tail { get; private set; }

        // 头部信息
        public int HeaderVersion { get; private set; }
        public uint KernelSize { get; private set; }
        public uint RamdiskSize { get; private set; }
        public uint SecondSize { get; private set; }
        public uint ExtraSize { get; private set; }
        public uint RecoveryDtboSize { get; private set; }
        public uint DtbSize { get; private set; }
        public uint BootconfigSize { get; private set; }
        public uint RecoveryDtboOffset { get; private set; }
        public uint PageSize { get; private set; }
        public uint OsVersion { get; private set; }
        public string Name { get; private set; }
        public string Cmdline { get; private set; }
        public string ExtraCmdline { get; private set; }

        // 格式信息
        public Format KernelFormat { get; private set; }
        public Format RamdiskFormat { get; private set; }
        public Format ExtraFormat { get; private set; }

        // Vendor ramdisk信息
        public bool HasVendorRamdiskTable => VendorRamdiskTable != null && VendorRamdiskTable.Length > 0;
        public List<VendorRamdiskEntry> VendorRamdiskEntries { get; } = new List<VendorRamdiskEntry>();

        public bool IsChromeOS => Flags[CHROMEOS_FLAG];
        public bool IsDhtb => Flags[DHTB_FLAG];
        public bool IsSeandroid => Flags[SEANDROID_FLAG];
        public bool IsLgBump => Flags[LG_BUMP_FLAG];
        public bool IsAvb1Signed => Flags[AVB1_SIGNED_FLAG]; // 添加便于检查签名的属性
        #endregion

        #region 构造函数
        public BootImage(string imagePath)
        {
            _filePath = imagePath;
            _fileData = File.ReadAllBytes(imagePath);
            Console.WriteLine($"Parsing boot image: [{imagePath}]");
            ParseBootImage();
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 将头部信息导出到文件
        /// </summary>
        public void DumpHeaderToFile()
        {
            using var writer = new StreamWriter(HEADER_FILE);
            if (!string.IsNullOrEmpty(Name))
                writer.WriteLine($"name={Name}");

            writer.WriteLine($"cmdline={Cmdline}{ExtraCmdline}");

            if (OsVersion != 0)
            {
                int version = (int)(OsVersion >> 11);
                int patchLevel = (int)(OsVersion & 0x7ff);

                int a = version >> 14 & 0x7f;
                int b = version >> 7 & 0x7f;
                int c = version & 0x7f;
                writer.WriteLine($"os_version={a}.{b}.{c}");

                int y = (patchLevel >> 4) + 2000;
                int m = patchLevel & 0xf;
                writer.WriteLine($"os_patch_level={y}-{m:D2}");
            }
        }

        /// <summary>
        /// 获取负载数据（用于签名）
        /// </summary>
        public byte[] GetPayload()
        {
            // 计算头部和各部分数据的总大小
            int headerSize = HeaderVersion switch
            {
                0 => 2048,
                1 => 2048 + 16 + 4,
                2 => 2048 + 16 + 4 + 4 + 4,
                _ => HeaderVersion <= 2 ? 2048 : 4096
            };

            int headerSpace = AlignTo(headerSize, (int)PageSize);
            int payloadSize = headerSpace;

            if (Kernel != null)
                payloadSize += AlignTo(Kernel.Length, (int)PageSize);

            if (Ramdisk != null)
                payloadSize += AlignTo(Ramdisk.Length, (int)PageSize);

            if (Second != null)
                payloadSize += AlignTo(Second.Length, (int)PageSize);

            if (Extra != null)
                payloadSize += AlignTo(Extra.Length, (int)PageSize);

            if (Dtb != null)
                payloadSize += AlignTo(Dtb.Length, (int)PageSize);

            // 创建负载数组并复制数据
            byte[] payload = new byte[payloadSize];
            Array.Copy(_fileData, payload, payloadSize);

            return payload;
        }

        /// <summary>
        /// 获取尾部数据（用于签名验证）
        /// </summary>
        public byte[] GetTail()
        {
            return Tail;
        }
        #endregion

        #region 私有方法
        private void ParseBootImage()
        {
            // 首先扫描文件寻找各种魔数
            for (int offset = 0; offset < _fileData.Length; offset++)
            {
                if (offset + 8 <= _fileData.Length)
                {
                    if (MatchMagic(offset, Encoding.ASCII.GetBytes(CHROMEOS_MAGIC)))
                    {
                        Flags[CHROMEOS_FLAG] = true;
                        offset += 65535; // 跳过ChromeOS头部
                        continue;
                    }
                    else if (MatchMagic(offset, Encoding.ASCII.GetBytes(DHTB_MAGIC)))
                    {
                        Flags[DHTB_FLAG] = true;
                        Flags[SEANDROID_FLAG] = true;
                        Console.WriteLine("DHTB_HDR");
                        offset += 512 - 1; // 跳过DHTB头部
                        continue;
                    }
                    else if (MatchMagic(offset, Encoding.ASCII.GetBytes(BOOT_MAGIC)))
                    {
                        if (ParseBootHeader(offset))
                            return;
                    }
                    else if (MatchMagic(offset, Encoding.ASCII.GetBytes(VENDOR_BOOT_MAGIC)))
                    {
                        if (ParseVendorBootHeader(offset))
                            return;
                    }
                }
            }

            throw new InvalidDataException("Invalid boot image!");
        }

        private bool ParseBootHeader(int offset)
        {
            // 解析引导镜像头部

            // 读取头部版本
            HeaderVersion = BitConverter.ToInt32(_fileData, offset + 40);
            Console.WriteLine($"HEADER_VER: [{HeaderVersion}]");

            // 读取内核大小
            KernelSize = BitConverter.ToUInt32(_fileData, offset + 8);
            Console.WriteLine($"KERNEL_SZ: [{KernelSize}]");

            // 读取ramdisk大小
            RamdiskSize = BitConverter.ToUInt32(_fileData, offset + 16);
            Console.WriteLine($"RAMDISK_SZ: [{RamdiskSize}]");

            // 读取second大小
            SecondSize = BitConverter.ToUInt32(_fileData, offset + 24);
            if (HeaderVersion < 3)
                Console.WriteLine($"SECOND_SZ: [{SecondSize}]");

            // 读取页大小
            PageSize = BitConverter.ToUInt32(_fileData, offset + 36);
            Console.WriteLine($"PAGESIZE: [{PageSize}]");

            // 根据版本读取其他字段...
            if (HeaderVersion == 0)
            {
                // v0特有字段
                ExtraSize = BitConverter.ToUInt32(_fileData, offset + 48);
                Console.WriteLine($"EXTRA_SZ: [{ExtraSize}]");
            }

            if (HeaderVersion == 1 || HeaderVersion == 2)
            {
                // v1/v2特有字段
                RecoveryDtboSize = BitConverter.ToUInt32(_fileData, offset + 1632);
                RecoveryDtboOffset = BitConverter.ToUInt32(_fileData, offset + 1624);
                Console.WriteLine($"RECOV_DTBO_SZ: [{RecoveryDtboSize}]");
            }

            if (HeaderVersion == 2)
            {
                // v2特有字段
                DtbSize = BitConverter.ToUInt32(_fileData, offset + 1648);
                Console.WriteLine($"DTB_SZ: [{DtbSize}]");
            }

            // 读取OS版本
            OsVersion = BitConverter.ToUInt32(_fileData, offset + 44);
            if (OsVersion != 0)
            {
                int version = (int)(OsVersion >> 11);
                int patchLevel = (int)(OsVersion & 0x7ff);

                int a = version >> 14 & 0x7f;
                int b = version >> 7 & 0x7f;
                int c = version & 0x7f;
                Console.WriteLine($"OS_VERSION: [{a}.{b}.{c}]");

                int y = (patchLevel >> 4) + 2000;
                int m = patchLevel & 0xf;
                Console.WriteLine($"OS_PATCH_LEVEL: [{y}-{m:D2}]");
            }

            // 读取名称
            Name = Encoding.ASCII.GetString(_fileData, offset + 32, 16).TrimEnd('\0');
            if (!string.IsNullOrEmpty(Name))
                Console.WriteLine($"NAME: [{Name}]");

            // 读取命令行
            Cmdline = Encoding.ASCII.GetString(_fileData, offset + 64, 512).TrimEnd('\0');
            ExtraCmdline = Encoding.ASCII.GetString(_fileData, offset + 576, 1024).TrimEnd('\0');
            Console.WriteLine($"CMDLINE: [{Cmdline}{ExtraCmdline}]");

            // 计算各部分的偏移
            int headerSize = CalculateHeaderSize();
            int currentOffset = offset + headerSize;
            currentOffset = AlignTo(currentOffset, (int)PageSize);

            // 提取内核
            if (KernelSize > 0)
            {
                Kernel = new byte[KernelSize];
                Buffer.BlockCopy(_fileData, currentOffset, Kernel, 0, (int)KernelSize);
                currentOffset += (int)KernelSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);

                // 检测内核格式
                KernelFormat = CheckFormatLg(Kernel);
                Console.WriteLine($"KERNEL_FMT: [{FormatUtils.FormatToName(KernelFormat)}]");

                // 尝试从内核中提取DTB
                int dtbOffset = FindDtbOffset(Kernel);
                if (dtbOffset > 0)
                {
                    KernelDtb = new byte[KernelSize - dtbOffset];
                    Buffer.BlockCopy(Kernel, dtbOffset, KernelDtb, 0, KernelDtb.Length);

                    // 调整内核大小
                    byte[] newKernel = new byte[dtbOffset];
                    Buffer.BlockCopy(Kernel, 0, newKernel, 0, dtbOffset);
                    Kernel = newKernel;
                    KernelSize = (uint)dtbOffset;

                    Console.WriteLine($"KERNEL_DTB_SZ: [{KernelDtb.Length}]");
                }
            }

            // 提取ramdisk
            if (RamdiskSize > 0)
            {
                Ramdisk = new byte[RamdiskSize];
                Buffer.BlockCopy(_fileData, currentOffset, Ramdisk, 0, (int)RamdiskSize);
                currentOffset += (int)RamdiskSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);

                // 检测ramdisk格式
                RamdiskFormat = CheckFormatLg(Ramdisk);
                Console.WriteLine($"RAMDISK_FMT: [{FormatUtils.FormatToName(RamdiskFormat)}]");
            }

            // 提取second
            if (SecondSize > 0)
            {
                Second = new byte[SecondSize];
                Buffer.BlockCopy(_fileData, currentOffset, Second, 0, (int)SecondSize);
                currentOffset += (int)SecondSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);
            }

            // 提取extra
            if (ExtraSize > 0)
            {
                Extra = new byte[ExtraSize];
                Buffer.BlockCopy(_fileData, currentOffset, Extra, 0, (int)ExtraSize);
                currentOffset += (int)ExtraSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);

                // 检测extra格式
                ExtraFormat = CheckFormatLg(Extra);
                Console.WriteLine($"EXTRA_FMT: [{FormatUtils.FormatToName(ExtraFormat)}]");
            }

            // 提取RecoveryDtbo
            if (RecoveryDtboSize > 0)
            {
                RecoveryDtbo = new byte[RecoveryDtboSize];
                Buffer.BlockCopy(_fileData, (int)RecoveryDtboOffset, RecoveryDtbo, 0, (int)RecoveryDtboSize);
                currentOffset = (int)RecoveryDtboOffset + (int)RecoveryDtboSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);
            }

            // 提取DTB
            if (DtbSize > 0)
            {
                Dtb = new byte[DtbSize];
                Buffer.BlockCopy(_fileData, currentOffset, Dtb, 0, (int)DtbSize);
                currentOffset += (int)DtbSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);
            }

            // 提取尾部数据
            if (currentOffset < _fileData.Length)
            {
                Tail = new byte[_fileData.Length - currentOffset];
                Buffer.BlockCopy(_fileData, currentOffset, Tail, 0, Tail.Length);

                // 检测特殊标志
                if (Tail.Length >= 16)
                {
                    if (MatchMagic(0, Encoding.ASCII.GetBytes(SEANDROID_MAGIC), Tail))
                    {
                        Console.WriteLine("SAMSUNG_SEANDROID");
                        Flags[SEANDROID_FLAG] = true;
                    }
                    else if (MatchMagic(0, Encoding.ASCII.GetBytes(LG_BUMP_MAGIC), Tail))
                    {
                        Console.WriteLine("LG_BUMP_IMAGE");
                        Flags[LG_BUMP_FLAG] = true;
                    }
                }
            }

            return true;
        }

        private bool ParseVendorBootHeader(int offset)
        {
            // 解析vendor boot镜像头部
            Console.WriteLine("Found VENDOR_BOOT_MAGIC");

            // 读取头部版本
            HeaderVersion = BitConverter.ToInt32(_fileData, offset + 8);
            Console.WriteLine($"VENDOR_HEADER_VER: [{HeaderVersion}]");

            if (HeaderVersion < 3)
            {
                Console.WriteLine("不支持的Vendor Boot镜像头部版本！");
                return false;
            }

            // 读取页大小
            PageSize = BitConverter.ToUInt32(_fileData, offset + 12);
            Console.WriteLine($"PAGESIZE: [{PageSize}]");

            // 读取ramdisk大小
            RamdiskSize = BitConverter.ToUInt32(_fileData, offset + 24);
            Console.WriteLine($"VENDOR_RAMDISK_SZ: [{RamdiskSize}]");

            // 读取名称
            Name = Encoding.ASCII.GetString(_fileData, offset + 2080, 16).TrimEnd('\0');
            if (!string.IsNullOrEmpty(Name))
                Console.WriteLine($"NAME: [{Name}]");

            // 读取命令行
            Cmdline = Encoding.ASCII.GetString(_fileData, offset + 28, 2048).TrimEnd('\0');
            Console.WriteLine($"VENDOR_CMDLINE: [{Cmdline}]");

            // 读取DTB大小
            DtbSize = BitConverter.ToUInt32(_fileData, offset + 2100);
            Console.WriteLine($"DTB_SZ: [{DtbSize}]");

            // 计算头部大小
            uint headerSize = (HeaderVersion >= 3) ? 2112u : 2048u;
            uint vendorRamdiskTableSize = 0;
            uint vendorRamdiskTableEntryNum = 0;
            uint vendorRamdiskTableEntrySize = 0;

            // 如果是V4，读取vendor ramdisk表信息
            if (HeaderVersion >= 4)
            {
                vendorRamdiskTableSize = BitConverter.ToUInt32(_fileData, offset + 2116);
                vendorRamdiskTableEntryNum = BitConverter.ToUInt32(_fileData, offset + 2120);
                vendorRamdiskTableEntrySize = BitConverter.ToUInt32(_fileData, offset + 2124);
                BootconfigSize = BitConverter.ToUInt32(_fileData, offset + 2128);

                Console.WriteLine($"VENDOR_RAMDISK_TABLE_SZ: [{vendorRamdiskTableSize}]");
                Console.WriteLine($"VENDOR_RAMDISK_TABLE_ENTRY_NUM: [{vendorRamdiskTableEntryNum}]");
                Console.WriteLine($"VENDOR_RAMDISK_TABLE_ENTRY_SIZE: [{vendorRamdiskTableEntrySize}]");
                Console.WriteLine($"BOOTCONFIG_SZ: [{BootconfigSize}]");
            }

            // 计算各部分的偏移
            int headerSpace = AlignTo((int)headerSize, (int)PageSize);
            int currentOffset = offset + headerSpace;

            // 提取ramdisk
            if (RamdiskSize > 0)
            {
                Ramdisk = new byte[RamdiskSize];
                Buffer.BlockCopy(_fileData, currentOffset, Ramdisk, 0, (int)RamdiskSize);
                currentOffset += (int)RamdiskSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);

                // 检测ramdisk格式
                RamdiskFormat = FormatUtils.CheckFormat(Ramdisk, (int)RamdiskSize);
                Console.WriteLine($"VENDOR_RAMDISK_FMT: [{FormatUtils.FormatToName(RamdiskFormat)}]");
            }

            // 提取DTB
            if (DtbSize > 0)
            {
                Dtb = new byte[DtbSize];
                Buffer.BlockCopy(_fileData, currentOffset, Dtb, 0, (int)DtbSize);
                currentOffset += (int)DtbSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);
            }

            // 提取Vendor Ramdisk表
            if (vendorRamdiskTableSize > 0)
            {
                VendorRamdiskTable = new byte[vendorRamdiskTableSize];
                Buffer.BlockCopy(_fileData, currentOffset, VendorRamdiskTable, 0, (int)vendorRamdiskTableSize);
                currentOffset += (int)vendorRamdiskTableSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);

                // 解析Vendor Ramdisk表
                ParseVendorRamdiskTable(vendorRamdiskTableEntryNum, vendorRamdiskTableEntrySize);
            }

            // 提取Bootconfig
            if (BootconfigSize > 0)
            {
                Bootconfig = new byte[BootconfigSize];
                Buffer.BlockCopy(_fileData, currentOffset, Bootconfig, 0, (int)BootconfigSize);
                currentOffset += (int)BootconfigSize;
                currentOffset = AlignTo(currentOffset, (int)PageSize);
            }

            return true;
        }

        private void ParseVendorRamdiskTable(uint entryNum, uint entrySize)
        {
            if (VendorRamdiskTable == null || VendorRamdiskTable.Length == 0 || entryNum == 0 || entrySize == 0)
                return;

            // 计算每个条目的大小，通常是VendorRamdiskTableEntryV4的大小
            int structSize = Marshal.SizeOf<VendorRamdiskTableEntryV4>();

            Console.WriteLine($"解析Vendor Ramdisk表: {entryNum}个条目");

            for (int i = 0; i < entryNum; i++)
            {
                int entryOffset = i * (int)entrySize;
                if (entryOffset + structSize > VendorRamdiskTable.Length)
                    break;

                // 从字节数组中提取结构
                IntPtr ptr = Marshal.AllocHGlobal(structSize);
                try
                {
                    Marshal.Copy(VendorRamdiskTable, entryOffset, ptr, structSize);
                    var entry = Marshal.PtrToStructure<VendorRamdiskTableEntryV4>(ptr);

                    string ramdiskName = Encoding.ASCII.GetString(entry.RamdiskName).TrimEnd('\0');
                    int ramdiskType = (int)entry.RamdiskType;
                    int ramdiskOffset = (int)entry.RamdiskOffset;
                    int ramdiskSize = (int)entry.RamdiskSize;

                    // 创建一个VendorRamdiskEntry对象并添加到列表中
                    var vendorEntry = new VendorRamdiskEntry
                    {
                        RamdiskName = ramdiskName,
                        RamdiskType = ramdiskType,
                        RamdiskOffset = ramdiskOffset,
                        RamdiskSize = ramdiskSize,
                        Format = Format.UNKNOWN
                    };

                    // 检查ramdisk的格式
                    if (ramdiskSize > 0 && ramdiskOffset + ramdiskSize <= Ramdisk.Length)
                    {
                        byte[] ramdiskData = new byte[ramdiskSize];
                        Buffer.BlockCopy(Ramdisk, ramdiskOffset, ramdiskData, 0, ramdiskSize);
                        vendorEntry.Format = FormatUtils.CheckFormat(ramdiskData, ramdiskSize);
                    }

                    VendorRamdiskEntries.Add(vendorEntry);
                    Console.WriteLine($"  Vendor Ramdisk[{i}]: 名称={ramdiskName}, 类型={GetRamdiskTypeName(ramdiskType)}, 大小={ramdiskSize}, 格式={FormatUtils.FormatToName(vendorEntry.Format)}");
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        private string GetRamdiskTypeName(int type)
        {
            return type switch
            {
                0 => "NONE",
                1 => "PLATFORM",
                2 => "RECOVERY",
                3 => "DLKM",
                _ => $"UNKNOWN({type})"
            };
        }

        private int CalculateHeaderSize()
        {
            // 根据头部版本计算头部大小
            return HeaderVersion switch
            {
                0 => 2048,  // v0 头部大小
                1 => 2048 + 16 + 4,  // v1 增加了recovery_dtbo相关字段
                2 => 2048 + 16 + 4 + 4 + 4,  // v2 增加了dtb相关字段
                _ => 2048  // 默认大小
            };
        }

        private int FindDtbOffset(byte[] buffer)
        {
            // 在内核数据中查找设备树二进制文件(DTB)的偏移
            byte[] magic = Encoding.ASCII.GetBytes(DTB_MAGIC);

            for (int i = 0; i <= buffer.Length - magic.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < magic.Length; j++)
                {
                    if (buffer[i + j] != magic[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }

        private Format CheckFormatLg(byte[] buffer)
        {
            // 检测数据格式，特别处理LZ4_LEGACY和LZ4_LG
            Format fmt = FormatUtils.CheckFormat(buffer, buffer.Length);

            if (fmt == Format.LZ4_LEGACY)
            {
                // 检查是否为LZ4_LG
                uint offset = 4;
                while (offset + 4 <= buffer.Length)
                {
                    uint blockSize = BitConverter.ToUInt32(buffer, (int)offset);
                    offset += 4;

                    if (offset + blockSize > buffer.Length)
                        return Format.LZ4_LG;

                    offset += blockSize;
                }
            }

            return fmt;
        }

        private bool MatchMagic(int offset, byte[] magic, byte[] buffer = null)
        {
            buffer ??= _fileData;

            if (offset + magic.Length > buffer.Length)
                return false;

            for (int i = 0; i < magic.Length; i++)
            {
                if (buffer[offset + i] != magic[i])
                    return false;
            }

            return true;
        }

        private int AlignTo(int value, int alignment)
        {
            return value + alignment - 1 & ~(alignment - 1);
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            _fileData = null;
            Kernel = null;
            KernelDtb = null;
            Ramdisk = null;
            Second = null;
            Extra = null;
            RecoveryDtbo = null;
            Dtb = null;
            Bootconfig = null;
            Tail = null;
        }
        #endregion
    }

    public class VendorRamdiskEntry
    {
        public string RamdiskName { get; set; }
        public int RamdiskType { get; set; }
        public int RamdiskOffset { get; set; }
        public int RamdiskSize { get; set; }
        public Format Format { get; set; }
    }
}