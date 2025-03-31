using DeviceTreeNode.Models;
using DeviceTreeNode.Nodes;
using System.CommandLine;

namespace DtbTool
{
    /// <summary>
    /// 设备树二进制文件处理工具
    /// </summary>
    public class DtbTool
    {
        private const int MaxPrintLen = 32;

        /// <summary>
        /// 主入口点
        /// </summary>
        public static int Main(string[] args)
        {
            // 创建根命令
            var rootCommand = new RootCommand("DTB文件处理工具");

            // 文件参数
            var fileArg = new Argument<FileInfo>(
                name: "file",
                description: "DTB文件路径")
            {
                Arity = ArgumentArity.ExactlyOne
            };
            rootCommand.AddArgument(fileArg);

            // print命令
            var printCommand = new Command("print", "打印DTB内容");
            var fstabOption = new Option<bool>(
                name: "-f",
                description: "仅打印fstab节点");
            printCommand.AddOption(fstabOption);

            printCommand.SetHandler((file, showOnlyFstab) =>
            {
                return Task.FromResult(PrintDtb(file.FullName, showOnlyFstab));
            }, fileArg, fstabOption);

            rootCommand.AddCommand(printCommand);

            // patch命令
            var patchCommand = new Command("patch", "移除verity/avb验证");
            patchCommand.SetHandler((file) =>
            {
                return Task.FromResult(PatchDtb(file.FullName));
            }, fileArg);

            rootCommand.AddCommand(patchCommand);

            // test命令
            var testCommand = new Command("test", "测试fstab状态");
            testCommand.SetHandler((file) =>
            {
                return Task.FromResult(TestDtb(file.FullName));
            }, fileArg);

            rootCommand.AddCommand(testCommand);

            // 解析命令行
            return rootCommand.InvokeAsync(args).Result;
        }

        /// <summary>
        /// 打印DTB内容
        /// </summary>
        private static int PrintDtb(string file, bool f)
        {
            try
            {
                using var dtb = MemoryMappedDtb.OpenRead(file);

                if (f)
                {
                    // 仅打印fstab节点
                    dtb.ForEachDtb((index, fdt) =>
                    {
                        var fstabNode = FindFstabNode(fdt);
                        if (fstabNode != null)
                        {
                            Console.WriteLine($"Found fstab in dtb.{index:D4}");
                            PrintNode(fstabNode);
                        }
                    });
                }
                else
                {
                    // 打印所有节点
                    dtb.ForEachDtb((index, fdt) =>
                    {
                        Console.WriteLine($"Printing dtb.{index:D4}");
                        var root = fdt.Root.Node;
                        PrintNode(root, "/");
                    });
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 修补DTB中的verity/avb
        /// </summary>
        private static int PatchDtb(string file)
        {
            try
            {
                bool keepVerity = Environment.GetEnvironmentVariable("KEEPVERITY") != null;

                using var dtb = MemoryMappedDtb.OpenReadWrite(file);
                bool patched = dtb.PatchVerity(keepVerity);

                return patched ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 测试fstab状态
        /// </summary>
        private static int TestDtb(string file)
        {
            try
            {
                using var dtb = MemoryMappedDtb.OpenRead(file);
                bool hasSystemRoot = dtb.TestFstabHasSystemRoot();

                // 与Rust版本保持一致：如果有/system_root则返回1
                return hasSystemRoot ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// 打印节点内容
        /// </summary>
        private static void PrintNode(FdtNode node, string nodeName = null)
        {
            void DoPrintNode(FdtNode currentNode, List<bool> depthSet, string name = null)
            {
                // 打印当前节点名称
                PrettyNode(depthSet);
                int depth = depthSet.Count;
                depthSet.Add(true);
                Console.WriteLine(name ?? currentNode.Name);

                // 收集属性和子节点
                var properties = currentNode.Properties().ToList();
                var children = currentNode.Children().ToList();

                // 打印属性
                for (int i = 0; i < properties.Count; i++)
                {
                    var prop = properties[i];
                    bool isLast = i == properties.Count - 1 && children.Count == 0;

                    if (isLast)
                        depthSet[depth] = false;

                    byte[] value = prop.Value;
                    int size = value.Length;

                    // 检查是否是字符串属性
                    bool isStr = !(size > 1 && value[0] == 0) &&
                                (size == 0 || value[size - 1] == 0) &&
                                value.All(c => c == 0 || c >= 32 && c < 127);

                    PrettyProp(depthSet);
                    if (isStr)
                    {
                        string strValue = prop.AsString();
                        Console.WriteLine($"[{prop.Name}]: [\"{strValue}\"]");
                    }
                    else if (size > MaxPrintLen)
                    {
                        Console.WriteLine($"[{prop.Name}]: <bytes>({size})");
                    }
                    else
                    {
                        Console.WriteLine($"[{prop.Name}]: [{BitConverter.ToString(value).Replace("-", " ")}]");
                    }
                }

                // 打印子节点
                for (int i = 0; i < children.Count; i++)
                {
                    if (i == children.Count - 1)
                        depthSet[depth] = false;

                    DoPrintNode(children[i], depthSet);
                }

                depthSet.RemoveAt(depthSet.Count - 1);
            }

            DoPrintNode(node, new List<bool>(), nodeName);
        }

        /// <summary>
        /// 打印节点的树形连接线
        /// </summary>
        private static void PrettyNode(List<bool> depthSet)
        {
            for (int i = 0; i < depthSet.Count; i++)
            {
                bool depth = depthSet[i];
                bool last = i == depthSet.Count - 1;

                if (depth)
                {
                    if (last)
                        Console.Write("├── ");
                    else
                        Console.Write("│   ");
                }
                else if (last)
                {
                    Console.Write("└── ");
                }
                else
                {
                    Console.Write("    ");
                }
            }
        }

        /// <summary>
        /// 打印属性的树形连接线
        /// </summary>
        private static void PrettyProp(List<bool> depthSet)
        {
            for (int i = 0; i < depthSet.Count; i++)
            {
                bool depth = depthSet[i];
                bool last = i == depthSet.Count - 1;

                if (depth)
                {
                    if (last)
                        Console.Write("│  ");
                    else
                        Console.Write("│   ");
                }
                else if (last)
                {
                    Console.Write("└─ ");
                }
                else
                {
                    Console.Write("    ");
                }
            }
        }

        /// <summary>
        /// 查找fstab节点
        /// </summary>
        private static FdtNode FindFstabNode(Fdt fdt)
        {
            return fdt.AllNodes().FirstOrDefault(n => n.Name == "fstab");
        }

        /// <summary>
        /// 显示帮助信息,翻译成英文以示友好
        /// </summary>
        public static void PrintUsage()
        {
            Console.Error.WriteLine(@"Usage: dtbtool <file> <action> [args...]
Do dtb related actions to <file>.

Supported actions:
  print [-f]
    Print all contents of dtb for debugging
    Specify [-f] to only print fstab nodes
  patch
    Search for fstab and remove verity/avb
    Modifications are done directly to the file in-place
    Configure with env variables: KEEPVERITY
  test
    Test the fstab's status
    Return values:
    0:valid    1:error");
        }
    }
}