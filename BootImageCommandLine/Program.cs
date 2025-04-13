using BootImage;

namespace BootImageCommandLine
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                ShowHelp();
                return;
            }

            string command = args[0].ToLower();
            string imagePath = args[1];

            switch (command)
            {
                case "unpack":
                case "u":
                    bool skipDecomp = args.Length > 2 && args[2].ToLower() == "--skip-decomp";
                    bool extractHeader = args.Length > 2 && args[2].ToLower() != "--no-header"
                                     || args.Length > 3 && args[3].ToLower() != "--no-header";
                    int result = BootImageUnpacker.Unpack(imagePath, skipDecomp, extractHeader);
                    Console.WriteLine($"Unpack completed with result code: {result}");
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("MagiskBoot-Sharp - Android引导镜像处理工具");
            Console.WriteLine("用法:");
            Console.WriteLine("  unpack|u <镜像文件> [--skip-decomp] [--no-header] - 解包引导镜像");
            Console.WriteLine("选项:");
            Console.WriteLine("  --skip-decomp  - 跳过解压缩");
            Console.WriteLine("  --no-header    - 不提取头部信息");
        }
    }
}