using Compress;

namespace CompressCommandLine
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                ShowHelp();
                return 1;
            }

            try
            {
                string command = args[0].ToLower();

                switch (command)
                {
                    case "compress":
                    case "c":
                        return HandleCompress(args);
                    case "decompress":
                    case "d":
                        return HandleDecompress(args);
                    case "formats":
                    case "f":
                        ShowFormats();
                        return 0;
                    case "help":
                    case "h":
                    case "?":
                        ShowHelp();
                        return 0;
                    default:
                        Console.WriteLine($"未知命令: {command}");
                        ShowHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"错误: {ex.Message}");
                return 1;
            }
        }

        static int HandleCompress(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("请提供输入文件路径");
                return 1;
            }

            string inputFile = args[1];
            string? outputFile = args.Length > 2 ? args[2] : null;

            // 判断是否指定了格式
            Format format = Format.UNKNOWN;
            if (args.Length > 3)
            {
                if (!TryParseFormat(args[3], out format))
                {
                    Console.WriteLine($"无效的格式: {args[3]}");
                    ShowFormats();
                    return 1;
                }
            }

            Console.WriteLine($"压缩文件 {inputFile}...");
            string? result = CompressionManager.CompressFile(inputFile, outputFile, format);

            if (result != null)
            {
                Console.WriteLine($"压缩成功: {result}");
                return 0;
            }
            else
            {
                Console.WriteLine("压缩失败");
                return 1;
            }
        }

        static int HandleDecompress(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("请提供输入文件路径");
                return 1;
            }

            string inputFile = args[1];
            string? outputFile = args.Length > 2 ? args[2] : null;

            Console.WriteLine($"解压文件 {inputFile}...");
            string? result;

            if (outputFile != null)
            {
                bool success = CompressionManager.AutoDecompressFile(inputFile, outputFile);
                result = success ? outputFile : null;
            }
            else
            {
                result = CompressionManager.AutoDecompressFile(inputFile);
            }

            if (result != null)
            {
                Console.WriteLine($"解压成功: {result}");
                return 0;
            }
            else
            {
                Console.WriteLine("解压失败");
                return 1;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("压缩解压缩测试工具");
            Console.WriteLine("用法:");
            Console.WriteLine("  compress|c <输入文件> [输出文件] [格式]  - 压缩文件");
            Console.WriteLine("  decompress|d <输入文件> [输出文件]      - 解压文件");
            Console.WriteLine("  formats|f                              - 显示支持的格式列表");
            Console.WriteLine("  help|h|?                               - 显示此帮助信息");
            Console.WriteLine();
            Console.WriteLine("示例:");
            Console.WriteLine("  compress test.txt                      - 压缩为test.txt.gz");
            Console.WriteLine("  compress test.txt test.xz xz           - 压缩为test.xz并使用xz格式");
            Console.WriteLine("  decompress test.gz                     - 解压test.gz为test");
        }

        static void ShowFormats()
        {
            Console.WriteLine("支持的格式:");

            var formatNames = Enum.GetNames(typeof(Format)).Where(f => f != "UNKNOWN");
            foreach (var formatName in formatNames)
            {
                // 尝试获取对应的Format枚举值
                if (Enum.TryParse<Format>(formatName, out var format))
                {
                    // 尝试获取扩展名
                    string extension = FormatUtils.FormatToExtension(format);
                    Console.WriteLine($"  {formatName.ToLower()} - {extension}");
                }
                else
                {
                    Console.WriteLine($"  {formatName.ToLower()}");
                }
            }
        }

        static bool TryParseFormat(string formatName, out Format format)
        {
            formatName = formatName.ToUpper();
            if (Enum.TryParse(formatName, out format))
            {
                return format != Format.UNKNOWN;
            }

            // 尝试将小写格式名转换为大写Format枚举
            switch (formatName.ToLower())
            {
                case "gz":
                case "gzip":
                    format = Format.GZIP;
                    return true;
                case "xz":
                    format = Format.XZ;
                    return true;
                case "lzma":
                    format = Format.LZMA;
                    return true;
                case "bzip2":
                case "bz2":
                    format = Format.BZIP2;
                    return true;
                case "lz4":
                    format = Format.LZ4;
                    return true;
                case "lz4_legacy":
                    format = Format.LZ4_LEGACY;
                    return true;
                case "lz4_lg":
                    format = Format.LZ4_LG;
                    return true;
                default:
                    format = Format.UNKNOWN;
                    return false;
            }
        }
    }
}