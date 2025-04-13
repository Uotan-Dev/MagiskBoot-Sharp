using System.Text;

namespace Compress
{
    // 文件格式枚举
    public enum Format
    {
        UNKNOWN,
        CHROMEOS,
        AOSP,
        AOSP_VENDOR,
        GZIP,
        ZOPFLI,
        LZOP,
        XZ,
        LZMA,
        BZIP2,
        LZ4,
        LZ4_LEGACY,
        LZ4_LG,
        MTK,
        DTB,
        DHTB,
        BLOB,
        ZIMAGE
    }

    public static class FormatUtils
    {
        // 定义魔术数字常量
        private static readonly byte[] CHROMEOS_MAGIC = Encoding.ASCII.GetBytes("CHROMEOS");
        private static readonly byte[] BOOT_MAGIC = Encoding.ASCII.GetBytes("ANDROID!");
        private static readonly byte[] VENDOR_BOOT_MAGIC = Encoding.ASCII.GetBytes("VNDRBOOT");
        private static readonly byte[] GZIP1_MAGIC = [0x1F, 0x8B, 0x08];
        private static readonly byte[] GZIP2_MAGIC = [0x1F, 0x8B, 0x0B];
        private static readonly byte[] LZOP_MAGIC = [0x89, 0x4C, 0x5A, 0x4F, 0x00, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] XZ_MAGIC = [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];
        private static readonly byte[] BZIP_MAGIC = [0x42, 0x5A, 0x68];
        private static readonly byte[] LZ41_MAGIC = [0x04, 0x22, 0x4D, 0x18];
        private static readonly byte[] LZ42_MAGIC = [0x02, 0x21, 0x4C, 0x18];
        private static readonly byte[] LZ4_LEG_MAGIC = [0x02, 0x21, 0x4C, 0x18];
        private static readonly byte[] MTK_MAGIC = Encoding.ASCII.GetBytes("MTKKERNELIMG");
        private static readonly byte[] DTB_MAGIC = [0xD0, 0x0D, 0xFE, 0xED];
        private static readonly byte[] DHTB_MAGIC = Encoding.ASCII.GetBytes("DHTB");
        private static readonly byte[] TEGRABLOB_MAGIC = Encoding.ASCII.GetBytes("BLOB");
        private static readonly byte[] ZIMAGE_MAGIC = [0x01, 0x1F, 0x1E, 0x1F];

        // 辅助方法：检查缓冲区是否匹配指定的魔术数字
        private static bool BufferMatch(byte[] buffer, byte[] magic)
        {
            if (buffer.Length < magic.Length) return false;

            for (int i = 0; i < magic.Length; i++)
            {
                if (buffer[i] != magic[i]) return false;
            }

            return true;
        }

        // 检查文件格式
        public static Format CheckFormat(byte[] buffer, int length)
        {
            // 确保缓冲区不为空且长度有效
            if (buffer == null || length <= 0)
                return Format.UNKNOWN;

            if (length >= CHROMEOS_MAGIC.Length && BufferMatch(buffer, CHROMEOS_MAGIC))
            {
                return Format.CHROMEOS;
            }
            else if (length >= BOOT_MAGIC.Length && BufferMatch(buffer, BOOT_MAGIC))
            {
                return Format.AOSP;
            }
            else if (length >= VENDOR_BOOT_MAGIC.Length && BufferMatch(buffer, VENDOR_BOOT_MAGIC))
            {
                return Format.AOSP_VENDOR;
            }
            else if (length >= GZIP1_MAGIC.Length && BufferMatch(buffer, GZIP1_MAGIC) ||
                     length >= GZIP2_MAGIC.Length && BufferMatch(buffer, GZIP2_MAGIC))
            {
                return Format.GZIP;
            }
            else if (length >= LZOP_MAGIC.Length && BufferMatch(buffer, LZOP_MAGIC))
            {
                return Format.LZOP;
            }
            else if (length >= XZ_MAGIC.Length && BufferMatch(buffer, XZ_MAGIC))
            {
                return Format.XZ;
            }
            else if (length >= 13 && buffer[0] == 0x5d && buffer[1] == 0x00 && buffer[2] == 0x00 &&
                     (buffer[12] == 0xff || buffer[12] == 0x00))
            {
                return Format.LZMA;
            }
            else if (length >= BZIP_MAGIC.Length && BufferMatch(buffer, BZIP_MAGIC))
            {
                return Format.BZIP2;
            }
            else if (length >= LZ41_MAGIC.Length && BufferMatch(buffer, LZ41_MAGIC) ||
                     length >= LZ42_MAGIC.Length && BufferMatch(buffer, LZ42_MAGIC))
            {
                return Format.LZ4;
            }
            else if (length >= LZ4_LEG_MAGIC.Length && BufferMatch(buffer, LZ4_LEG_MAGIC))
            {
                return Format.LZ4_LEGACY;
            }
            else if (length >= MTK_MAGIC.Length && BufferMatch(buffer, MTK_MAGIC))
            {
                return Format.MTK;
            }
            else if (length >= DTB_MAGIC.Length && BufferMatch(buffer, DTB_MAGIC))
            {
                return Format.DTB;
            }
            else if (length >= DHTB_MAGIC.Length && BufferMatch(buffer, DHTB_MAGIC))
            {
                return Format.DHTB;
            }
            else if (length >= TEGRABLOB_MAGIC.Length && BufferMatch(buffer, TEGRABLOB_MAGIC))
            {
                return Format.BLOB;
            }
            else if (length >= 0x28)
            {
                byte[] zimageMagicBytes = new byte[ZIMAGE_MAGIC.Length];
                Buffer.BlockCopy(buffer, 0x24, zimageMagicBytes, 0, ZIMAGE_MAGIC.Length);
                if (BufferMatch(zimageMagicBytes, ZIMAGE_MAGIC))
                {
                    return Format.ZIMAGE;
                }
            }

            return Format.UNKNOWN;
        }

        // 格式到名称的映射
        public static string FormatToName(Format format)
        {
            return format switch
            {
                Format.GZIP => "gzip",
                Format.ZOPFLI => "zopfli",
                Format.LZOP => "lzop",
                Format.XZ => "xz",
                Format.LZMA => "lzma",
                Format.BZIP2 => "bzip2",
                Format.LZ4 => "lz4",
                Format.LZ4_LEGACY => "lz4_legacy",
                Format.LZ4_LG => "lz4_lg",
                Format.DTB => "dtb",
                Format.ZIMAGE => "zimage",
                _ => "raw",
            };
        }

        // 格式到扩展名的映射
        public static string FormatToExtension(Format format)
        {
            return format switch
            {
                Format.GZIP or Format.ZOPFLI => ".gz",
                Format.LZOP => ".lzo",
                Format.XZ => ".xz",
                Format.LZMA => ".lzma",
                Format.BZIP2 => ".bz2",
                Format.LZ4 or Format.LZ4_LEGACY or Format.LZ4_LG => ".lz4",
                _ => "",
            };
        }

        // 名称到格式的映射
        public static Format NameToFormat(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Format.UNKNOWN;

            return name.ToLower() switch
            {
                "gzip" => Format.GZIP,
                "zopfli" => Format.ZOPFLI,
                "xz" => Format.XZ,
                "lzma" => Format.LZMA,
                "bzip2" => Format.BZIP2,
                "lz4" => Format.LZ4,
                "lz4_legacy" => Format.LZ4_LEGACY,
                "lz4_lg" => Format.LZ4_LG,
                _ => Format.UNKNOWN,
            };
        }
        public static bool IsCompressedFormat(this Format format)
        {
            // Add logic to determine if the format is compressed
            return format == Format.GZIP || format == Format.LZ4 || format == Format.XZ ||
                   format == Format.LZMA || format == Format.BZIP2 ||
                   format == Format.LZ4_LEGACY || format == Format.LZ4_LG || format == Format.ZIMAGE;
        }

    }
}
