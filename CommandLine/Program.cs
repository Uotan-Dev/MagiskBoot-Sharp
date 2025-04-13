using BootImage;
using Compress;
using DtbTool;
using System.Text;

namespace CommandLine
{
    class Program
    {
        const string NEW_BOOT = "new-boot.img";
        const string HEADER_FILE = "header";
        const string KERNEL_FILE = "kernel";
        const string RAMDISK_FILE = "ramdisk.cpio";
        const string SECOND_FILE = "second";
        const string KER_DTB_FILE = "kernel_dtb";
        const string EXTRA_FILE = "extra";
        const string RECV_DTBO_FILE = "recovery_dtbo";
        const string DTB_FILE = "dtb";
        const string BOOTCONFIG_FILE = "bootconfig";
        const string VND_RAMDISK_DIR = "vendor_ramdisk";

        static void PrintFormats()
        {
            Console.WriteLine("gzip xz lzma bzip2 lz4 lz4_legacy lz4_lg ");
        }


        static void PrintUsage(string arg0)
        {
            Console.Error.WriteLine($@"MagiskBoot-Sharp - Boot Image Modification Tool

用法: {arg0} <action> [args...]

支持的操作:
  unpack [-n] [-h] <bootimg>
    解包 <bootimg> 到它的各个组件，每个组件都会保存到
    当前目录下对应的文件名。
    支持的组件: kernel, kernel_dtb, ramdisk.cpio, second,
    dtb, extra, 和 recovery_dtbo.
    默认情况下，每个组件将会被自动解压缩。
    如果提供了 '-n' 参数，所有解压缩操作将被跳过；
    每个组件将保持原始格式。
    如果提供了 '-h' 参数，引导镜像的头部信息将被
    保存到 'header' 文件，可以在重打包时用来修改头部配置。
    返回值:
    0:成功    1:错误    2:chromeos

  repack [-n] <origbootimg> [outbootimg]
    使用当前目录中的文件重新打包引导镜像
    到 [outbootimg]，如果未指定则打包到 'new-boot.img'。
    当前目录应该只包含 [outbootimg] 所需的文件，
    否则可能会生成错误的 [outbootimg]。
    <origbootimg> 是用来解包组件的原始引导镜像。
    默认情况下，每个组件将使用从 <origbootimg> 中检测到的
    对应格式自动压缩。如果当前目录中的组件文件已经被
    压缩，则不会对该组件进行额外的压缩。
    如果提供了 '-n' 参数，所有压缩操作将被跳过。
    如果环境变量 PATCHVBMETAFLAG 设置为 true，
    引导镜像 vbmeta 头部中的所有禁用标志将被设置。

  verify <bootimg> [x509.pem]
    检查引导镜像是否带有 AVB 1.0 签名。
    可以选择提供证书来验证镜像是否使用公钥证书签名。
    返回值:
    0:有效    1:错误

  sign <bootimg> [name] [x509.pem pk8]
    使用 AVB 1.0 签名对 <bootimg> 进行签名。
    可以选择提供镜像的名称（默认: '/boot'）。
    可以选择提供用于签名的证书/私钥对。
    如果未提供证书/私钥对，将使用内置在
    程序中的 AOSP verity 密钥。

  extract <payload.bin> [partition] [outfile]
    从 <payload.bin> 中提取 [partition] 到 [outfile]。
    如果未指定 [outfile]，则输出到 '[partition].img'。
    如果未指定 [partition]，则尝试提取 'init_boot'
    或 'boot'。选择哪个分区可以通过查看 'init_boot.img'
    或 'boot.img' 是否存在来确定。
    <payload.bin> 可以是 '-' 表示从标准输入读取。

  hexpatch <file> <hexpattern1> <hexpattern2>
    在 <file> 中搜索 <hexpattern1>，并替换为 <hexpattern2>

  cpio <incpio> [commands...]
    对 <incpio> 执行 cpio 命令（修改将直接应用于文件）。
    每个命令都是单独的参数；为每个命令添加引号。
    使用 ""cpio --help"" 查看支持的命令。

  dtb <file> <action> [args...]
    对 <file> 执行 dtb 相关操作。
    使用 ""dtb --help"" 查看支持的操作。

  split [-n] <file>
    将 image.*-dtb 分割为 kernel + kernel_dtb。
    如果提供了 '-n' 参数，解压缩操作将被跳过；
    内核将保持原始格式。

  sha1 <file>
    打印 <file> 的 SHA1 校验和

  cleanup
    清理当前工作目录

  compress[=format] <infile> [outfile]
    使用 [format] 格式压缩 <infile> 到 [outfile]。
    <infile>/[outfile] 可以是 '-' 表示从标准输入/标准输出。
    如果未指定 [format]，则使用 gzip。
    如果未指定 [outfile]，则 <infile> 将被替换为
    带有匹配文件扩展名的文件。
    支持的格式: gzip xz lzma bzip2 lz4 lz4_legacy lz4_lg ");

            Console.Error.WriteLine(@"

  decompress <infile> [outfile]
    检测格式并解压缩 <infile> 到 [outfile]。
    <infile>/[outfile] 可以是 '-' 表示从标准输入/标准输出。
    如果未指定 [outfile]，则 <infile> 将被替换为
    去掉其压缩格式文件扩展名的文件。
    支持的格式: gzip zopfli xz lzma bzip2 lz4 lz4_legacy lz4_lg ");

            Console.Error.WriteLine("\n");
            Environment.Exit(1);
        }

        static int Main(string[] args)
        {
            // 设置控制台编码为UTF-8
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            if (args.Length < 1)
                PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));

            string action = args[0];

            // 兼容性处理：跳过'--'前缀
            if (action.StartsWith("--"))
                action = action.Substring(2);

            if (action == "cleanup")
            {
                Console.Error.WriteLine("清理中...");
                if (File.Exists(HEADER_FILE)) File.Delete(HEADER_FILE);
                if (File.Exists(KERNEL_FILE)) File.Delete(KERNEL_FILE);
                if (File.Exists(RAMDISK_FILE)) File.Delete(RAMDISK_FILE);
                if (File.Exists(SECOND_FILE)) File.Delete(SECOND_FILE);
                if (File.Exists(KER_DTB_FILE)) File.Delete(KER_DTB_FILE);
                if (File.Exists(EXTRA_FILE)) File.Delete(EXTRA_FILE);
                if (File.Exists(RECV_DTBO_FILE)) File.Delete(RECV_DTBO_FILE);
                if (File.Exists(DTB_FILE)) File.Delete(DTB_FILE);
                if (File.Exists(BOOTCONFIG_FILE)) File.Delete(BOOTCONFIG_FILE);
                if (Directory.Exists(VND_RAMDISK_DIR)) Directory.Delete(VND_RAMDISK_DIR, true);
            }
            else if (args.Length > 1 && action == "sha1")
            {
                byte[] data = File.ReadAllBytes(args[1]);
                byte[] hash = System.Security.Cryptography.SHA1.Create().ComputeHash(data);
                foreach (byte b in hash)
                {
                    Console.Write($"{b:x2}");
                }
                Console.WriteLine();
            }
            else if (args.Length > 1 && action == "split")
            {
                if (args[1] == "-n")
                {
                    if (args.Length == 2)
                        PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));
                    return BootImageTool.SplitImageDtb(args[2], true) ? 0 : 1;
                }
                else
                {
                    return BootImageTool.SplitImageDtb(args[1]) ? 0 : 1;
                }
            }
            else if (args.Length > 1 && action == "unpack")
            {
                int idx = 1;
                bool nodecomp = false;
                bool hdr = false;

                // 解析参数
                for (; ; )
                {
                    if (idx >= args.Length)
                        PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));

                    if (!args[idx].StartsWith("-"))
                        break;

                    foreach (char flag in args[idx].Substring(1))
                    {
                        if (flag == 'n')
                            nodecomp = true;
                        else if (flag == 'h')
                            hdr = true;
                        else
                            PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));
                    }
                    idx++;
                }

                return BootImageTool.Unpack(args[idx], nodecomp, hdr);
            }
            else if (args.Length > 1 && action == "repack")
            {
                if (args[1] == "-n")
                {
                    if (args.Length == 2)
                        PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));

                    return BootImageTool.Repack(args[2], args.Length > 3 ? args[3] : NEW_BOOT, true) ? 0 : 1;
                }
                else
                {
                    return BootImageTool.Repack(args[1], args.Length > 2 ? args[2] : NEW_BOOT) ? 0 : 1;
                }
            }
            else if (args.Length > 1 && action == "verify")
            {
                return BootImageTool.Verify(args[1], args.Length > 2 ? args[2] : null) ? 0 : 1;
            }
            else if (args.Length > 1 && action == "sign")
            {
                if (args.Length == 4)
                    PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));

                return BootImageTool.Sign(
                    args[1],
                    args.Length > 2 ? args[2] : "/boot",
                    args.Length > 4 ? args[3] : null,
                    args.Length > 4 ? args[4] : null) ? 0 : 1;
            }
            else if (args.Length > 1 && action == "decompress")
            {
                // 处理标准输入输出
                if (args[1] == "-" && args.Length > 2 && args[2] == "-")
                {
                    // 从标准输入读取，输出到标准输出
                    using Stream stdin = Console.OpenStandardInput();
                    using Stream stdout = Console.OpenStandardOutput();
                    return CompressionManager.AutoDecompress(stdin, stdout) ? 0 : 1;
                }
                else if (args[1] == "-" && args.Length > 2)
                {
                    // 从标准输入读取，输出到文件
                    using Stream stdin = Console.OpenStandardInput();
                    using Stream output = File.Create(args[2]);
                    return CompressionManager.AutoDecompress(stdin, output) ? 0 : 1;
                }
                else if (args.Length > 2 && args[2] == "-")
                {
                    // 从文件读取，输出到标准输出
                    using Stream input = File.OpenRead(args[1]);
                    using Stream stdout = Console.OpenStandardOutput();
                    return CompressionManager.AutoDecompress(input, stdout) ? 0 : 1;
                }
                else if (args.Length > 2)
                {
                    // 从文件读取，输出到文件
                    return CompressionManager.AutoDecompressFile(args[1], args[2]) ? 0 : 1;
                }
                else
                {
                    // 自动生成输出文件名
                    string? outputFile = CompressionManager.AutoDecompressFile(args[1]);
                    return outputFile != null ? 0 : 1;
                }
            }
            else if (args.Length > 1 && action.StartsWith("compress"))
            {
                string format = "gzip";
                if (action.Length > 8 && action[8] == '=')
                {
                    format = action.Substring(9);
                }

                Format compressionFormat = FormatUtils.NameToFormat(format);
                if (compressionFormat == Format.UNKNOWN)
                {
                    Console.Error.WriteLine($"不支持的压缩格式: {format}");
                    return 1;
                }

                // 处理标准输入输出
                if (args[1] == "-" && args.Length > 2 && args[2] == "-")
                {
                    // 从标准输入读取，输出到标准输出
                    using Stream stdin = Console.OpenStandardInput();
                    using Stream stdout = Console.OpenStandardOutput();
                    return CompressionManager.Compress(stdin, stdout, compressionFormat) ? 0 : 1;
                }
                else if (args[1] == "-" && args.Length > 2)
                {
                    // 从标准输入读取，输出到文件
                    using Stream stdin = Console.OpenStandardInput();
                    using Stream output = File.Create(args[2]);
                    return CompressionManager.Compress(stdin, output, compressionFormat) ? 0 : 1;
                }
                else if (args.Length > 2 && args[2] == "-")
                {
                    // 从文件读取，输出到标准输出
                    using Stream input = File.OpenRead(args[1]);
                    using Stream stdout = Console.OpenStandardOutput();
                    return CompressionManager.Compress(input, stdout, compressionFormat) ? 0 : 1;
                }
                else if (args.Length > 2)
                {
                    // 从文件读取，输出到文件
                    return CompressionManager.CompressFile(args[1], args[2], compressionFormat) != null ? 0 : 1;
                }
                else
                {
                    // 自动生成输出文件名
                    return CompressionManager.CompressFile(args[1], null, compressionFormat) != null ? 0 : 1;
                }
            }

            else if (args.Length > 3 && action == "hexpatch")
            {
                // Convert the string arguments to byte arrays using HexToByte method
                byte[] fileBytes = File.ReadAllBytes(args[1]);
                byte[] fromBytes = HexPatch.Patch.HexToByte(args[2]);
                byte[] toBytes = HexPatch.Patch.HexToByte(args[3]);

                return HexPatch.Patch.HexPatch(fileBytes, fromBytes, toBytes) ? 0 : 1;
            }

            else if (args.Length > 1 && action == "cpio")
            {
                return Cpio.CpioArchive.ProcessCommands(args[1], args.AsSpan(2).ToArray()) ? 0 : 1;
            }
            else if (args.Length > 1 && action == "dtb")
            {
                // 创建新的参数数组，将dtb命令后的所有参数传递给DtbTool
                string[] dtbArgs = args.Skip(1).ToArray();
                return DtbTool.DtbTool.Main(dtbArgs);
            }
            else if (args.Length > 1 && action == "info")
            {
                return BootImageTool.PrintInfo(args[1]) ? 0 : 1;
            }
            else
            {
                PrintUsage(Path.GetFileName(Environment.GetCommandLineArgs()[0]));
            }

            return 0;
        }
    }
}
