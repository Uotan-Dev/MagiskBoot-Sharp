using System;
using System.Linq;

namespace Cpio
{
    /// <summary>
    /// CPIO 文件命令行处理工具
    /// </summary>
    public static class CpioArchive
    {
        /// <summary>
        /// 处理CPIO的命令行命令
        /// </summary>
        /// <param name="cpioPath">CPIO文件路径</param>
        /// <param name="commands">要执行的命令数组</param>
        /// <returns>操作是否成功</returns>
        public static bool ProcessCommands(string cpioPath, string[] commands)
        {
            try
            {
                // 加载CPIO文件
                var cpio = Cpio.LoadFromFile(cpioPath);
                bool modified = false;
                bool shouldDump = false;
                
                if (commands.Length == 0)
                {
                    // 如果没有命令，显示cpio内容
                    foreach (var entry in cpio.List())
                    {
                        Console.WriteLine(entry);
                    }
                    return true;
                }

                // 处理命令
                for (int i = 0; i < commands.Length; i++)
                {
                    string cmd = commands[i];

                    switch (cmd)
                    {
                        case "rm":
                            if (i + 1 >= commands.Length)
                            {
                                Console.Error.WriteLine("错误: rm命令缺少参数");
                                return false;
                            }
                            string rmPath = commands[++i];
                            bool recursive = false;
                            
                            if (i + 1 < commands.Length && commands[i + 1] == "-r")
                            {
                                recursive = true;
                                i++;
                            }
                            
                            cpio.Remove(rmPath, recursive);
                            modified = true;
                            shouldDump = true;
                            break;

                        case "mkdir":
                            if (i + 2 >= commands.Length)
                            {
                                Console.Error.WriteLine("错误: mkdir命令缺少参数，格式: mkdir <权限> <目录路径>");
                                return false;
                            }
                            string modeStr = commands[++i];
                            string dirPath = commands[++i];
                            
                            // 将权限转换为八进制数字
                            if (!TryParseOctalMode(modeStr, out uint mode))
                            {
                                Console.Error.WriteLine($"错误: 无效的权限模式: {modeStr}");
                                return false;
                            }
                            
                            cpio.Mkdir(mode, dirPath);
                            modified = true;
                            shouldDump = true;
                            break;

                        case "add":
                            if (i + 3 >= commands.Length)
                            {
                                Console.Error.WriteLine("错误: add命令缺少参数，格式: add <权限> <目标路径> <源文件路径>");
                                return false;
                            }
                            string addModeStr = commands[++i];
                            string dstPath = commands[++i];
                            string srcPath = commands[++i];
                            
                            // 将权限转换为八进制数字
                            if (!TryParseOctalMode(addModeStr, out uint addMode))
                            {
                                Console.Error.WriteLine($"错误: 无效的权限模式: {addModeStr}");
                                return false;
                            }
                            
                            cpio.Add(addMode, dstPath, srcPath);
                            modified = true;
                            shouldDump = true;
                            break;

                        case "mv":
                            if (i + 2 >= commands.Length)
                            {
                                Console.Error.WriteLine("错误: mv命令缺少参数，格式: mv <源路径> <目标路径>");
                                return false;
                            }
                            string fromPath = commands[++i];
                            string toPath = commands[++i];
                            
                            cpio.Move(fromPath, toPath);
                            modified = true;
                            shouldDump = true;
                            break;

                        case "extract":
                            if (i + 1 >= commands.Length)
                            {
                                // 提取所有文件
                                cpio.Extract();
                            }
                            else if (i + 2 >= commands.Length)
                            {
                                // 提取指定文件到相同路径
                                string extractPath = commands[++i];
                                cpio.Extract(extractPath, null);
                            }
                            else
                            {
                                // 提取指定文件到指定路径
                                string extractPath = commands[++i];
                                string outputPath = commands[++i];
                                cpio.Extract(extractPath, outputPath);
                            }
                            break;

                        case "exists":
                            if (i + 1 >= commands.Length)
                            {
                                Console.Error.WriteLine("错误: exists命令缺少参数");
                                return false;
                            }
                            string checkPath = commands[++i];
                            
                            bool exists = cpio.Exists(checkPath);
                            Console.WriteLine(exists ? "1" : "0");
                            break;

                        case "ls":
                            string lsPath = "/";
                            
                            if (i + 1 < commands.Length && !commands[i + 1].StartsWith("-"))
                            {
                                lsPath = commands[++i];
                            }
                            
                            if (i + 1 < commands.Length && commands[i + 1] == "-r")
                            {
                                recursive = true;
                                i++;
                            }
                            
                            foreach (var entry in cpio.List(lsPath))
                            {
                                Console.WriteLine(entry);
                            }
                            break;

                        case "--help":
                        case "help":
                            PrintHelp();
                            return true;

                        default:
                            Console.Error.WriteLine($"错误: 未知命令: {cmd}");
                            PrintHelp();
                            return false;
                    }
                }

                // 如果文件被修改，保存回同一个文件
                if (modified && shouldDump)
                {
                    cpio.Dump(cpioPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试将八进制模式字符串解析为uint值
        /// </summary>
        private static bool TryParseOctalMode(string modeStr, out uint mode)
        {
            mode = 0;
            
            // 去除可能的前缀"0"或"0o"
            if (modeStr.StartsWith("0o"))
                modeStr = modeStr.Substring(2);
            else if (modeStr.StartsWith("0") && modeStr.Length > 1)
                modeStr = modeStr.Substring(1);
                
            try
            {
                // 解析八进制
                mode = Convert.ToUInt32(modeStr, 8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 打印帮助信息
        /// </summary>
        private static void PrintHelp()
        {
            Console.Error.WriteLine(@"CPIO 归档操作:

用法:
  cpio <incpio> [commands...]

命令:
  rm [-r] <entry>         删除条目，可选择递归删除
  mkdir <mode> <dir>      在CPIO中创建目录
  add <mode> <entry> <file>  从文件系统添加文件到CPIO
  mv <from> <to>          移动/重命名条目
  extract [from [to]]     提取文件，可指定源和目标
  exists <entry>          检查条目是否存在，返回0或1
  ls [-r] [path]          列出条目，可选择递归列出

模式应指定为八进制权限值，例如:
  0755 = rwxr-xr-x
  0644 = rw-r--r--
  0700 = rwx------");
        }
    }
}