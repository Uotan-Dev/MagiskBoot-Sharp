using Cpio.Extensions;
using Cpio.Helpers;
using Cpio.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ByteConverter = Cpio.Helpers.ByteConverter;

namespace Cpio
{
    /// <summary>
    /// CPIO 文件格式处理类，用于处理 Linux ramdisk 文件
    /// </summary>
    public class Cpio
    {
        /// <summary>
        /// 存储 CPIO 条目的有序字典
        /// </summary>
        private SortedDictionary<string, CpioEntry> entries;

        /// <summary>
        /// 获取所有条目的只读集合
        /// </summary>
        public IReadOnlyDictionary<string, CpioEntry> Entries => entries;

        /// <summary>
        /// 构造一个空的 CPIO 对象
        /// </summary>
        public Cpio()
        {
            entries = new SortedDictionary<string, CpioEntry>();
        }

        /// <summary>
        /// 从二进制数据加载 CPIO 文件
        /// </summary>
        /// <param name="data">包含 CPIO 格式数据的字节数组</param>
        /// <returns>加载的 CPIO 对象</returns>
        public static Cpio LoadFromData(byte[] data)
        {
            var cpio = new Cpio();
            int pos = 0;

            while (pos < data.Length)
            {
                // 解析 CPIO 头
                int hdrSize = Marshal.SizeOf<CpioHeader>();
                if (pos + hdrSize > data.Length)
                    throw new Exception("Unexpected end of CPIO data");

                var hdr = ByteConverter.BytesToStruct<CpioHeader>(data, pos, hdrSize);

                // 验证 magic 数字
                string magic = Encoding.ASCII.GetString(hdr.Magic);
                if (magic != "070701")
                    throw new Exception($"Invalid CPIO magic: {magic}");

                pos += hdrSize;

                // 读取文件名
                uint nameSz = ByteConverter.ParseHexField(hdr.NameSize);
                if (pos + nameSz > data.Length)
                    throw new Exception("Unexpected end of CPIO data");

                string name = Encoding.UTF8.GetString(data, pos, (int)nameSz - 1); // 减1去掉NULL终止符
                pos += (int)nameSz;
                pos = ByteConverter.Align4(pos);

                // 跳过特殊条目
                if (name == "." || name == "..")
                    continue;

                // 检测尾部标记
                if (name == "TRAILER!!!")
                {
                    // 尝试查找下一个 CPIO 文件的开始
                    int nextMagicPos = ByteConverter.FindBytes(data, Encoding.ASCII.GetBytes("070701"), pos);
                    if (nextMagicPos >= 0)
                        pos = nextMagicPos;
                    else
                        break;
                    continue;
                }

                // 读取文件内容
                uint fileSz = ByteConverter.ParseHexField(hdr.FileSize);
                if (pos + fileSz > data.Length)
                    throw new Exception("Unexpected end of CPIO data");

                var entry = new CpioEntry
                {
                    Mode = ByteConverter.ParseHexField(hdr.Mode),
                    Uid = ByteConverter.ParseHexField(hdr.Uid),
                    Gid = ByteConverter.ParseHexField(hdr.Gid),
                    RdevMajor = ByteConverter.ParseHexField(hdr.RdevMajor),
                    RdevMinor = ByteConverter.ParseHexField(hdr.RdevMinor),
                    Data = new byte[fileSz]
                };

                // 复制文件数据
                if (fileSz > 0)
                    Buffer.BlockCopy(data, pos, entry.Data, 0, (int)fileSz);

                pos += (int)fileSz;
                cpio.entries[name] = entry;
                pos = ByteConverter.Align4(pos);
            }

            return cpio;
        }

        /// <summary>
        /// 从文件加载 CPIO 档案
        /// </summary>
        /// <param name="path">CPIO 文件路径</param>
        /// <returns>加载的 CPIO 对象</returns>
        public static Cpio LoadFromFile(string path)
        {
            Console.Error.WriteLine($"Loading cpio: [{path}]");
            byte[] data = File.ReadAllBytes(path);
            return LoadFromData(data);
        }

        /// <summary>
        /// 将 CPIO 内容导出到文件
        /// </summary>
        /// <param name="path">导出路径</param>
        public void Dump(string path)
        {
            Console.Error.WriteLine($"Dumping cpio: [{path}]");
            using (var file = File.Create(path))
            {
                int pos = 0;
                long inode = 300000;

                foreach (var pair in entries)
                {
                    string name = pair.Key;
                    var entry = pair.Value;

                    // 写入 CPIO 头
                    string header = string.Format(
                        "070701{0:x8}{1:x8}{2:x8}{3:x8}{4:x8}{5:x8}{6:x8}{7:x8}{8:x8}{9:x8}{10:x8}{11:x8}{12:x8}",
                        inode,
                        entry.Mode,
                        entry.Uid,
                        entry.Gid,
                        1,
                        0,
                        entry.Data.Length,
                        0,
                        0,
                        entry.RdevMajor,
                        entry.RdevMinor,
                        name.Length + 1,
                        0);

                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    file.Write(headerBytes, 0, headerBytes.Length);
                    pos += headerBytes.Length;

                    // 写入文件名
                    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                    file.Write(nameBytes, 0, nameBytes.Length);
                    pos += nameBytes.Length;

                    // 写入结束符
                    file.WriteByte(0);
                    pos += 1;

                    // 对齐到4字节边界
                    file.WriteZeros(ByteConverter.Align4(pos) - pos);
                    pos = ByteConverter.Align4(pos);

                    // 写入文件内容
                    file.Write(entry.Data, 0, entry.Data.Length);
                    pos += entry.Data.Length;

                    // 对齐到4字节边界
                    file.WriteZeros(ByteConverter.Align4(pos) - pos);
                    pos = ByteConverter.Align4(pos);

                    inode++;
                }

                // 写入结束标记
                string trailer = string.Format(
                    "070701{0:x8}{1:x8}{2:x8}{3:x8}{4:x8}{5:x8}{6:x8}{7:x8}{8:x8}{9:x8}{10:x8}{11:x8}{12:x8}",
                    inode, 0755, 0, 0, 1, 0, 0, 0, 0, 0, 0, 11, 0);

                byte[] trailerBytes = Encoding.ASCII.GetBytes(trailer);
                file.Write(trailerBytes, 0, trailerBytes.Length);
                pos += trailerBytes.Length;

                // 写入结束名称
                byte[] trailerName = Encoding.ASCII.GetBytes("TRAILER!!!\0");
                file.Write(trailerName, 0, trailerName.Length);
                pos += trailerName.Length;

                // 最终对齐
                file.WriteZeros(ByteConverter.Align4(pos) - pos);
            }
        }

        /// <summary>
        /// 删除路径条目，可选择递归删除
        /// </summary>
        /// <param name="path">要删除的路径</param>
        /// <param name="recursive">是否递归删除</param>
        public void Remove(string path, bool recursive = false)
        {
            string normalizedPath = PathHelper.NormalizePath(path);

            if (entries.Remove(normalizedPath))
            {
                Console.Error.WriteLine($"Removed entry [{normalizedPath}]");
            }

            if (recursive)
            {
                string prefix = normalizedPath + "/";
                var keysToRemove = entries.Keys
                    .Where(k => k.StartsWith(prefix))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    entries.Remove(key);
                    Console.Error.WriteLine($"Removed entry [{key}]");
                }
            }
        }

        /// <summary>
        /// 提取指定条目到文件
        /// </summary>
        /// <param name="path">要提取的条目路径</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <exception cref="FileNotFoundException">找不到指定条目时抛出</exception>
        public void ExtractEntry(string path, string outputPath)
        {
            string normalizedPath = PathHelper.NormalizePath(path);

            if (!entries.TryGetValue(normalizedPath, out var entry))
                throw new FileNotFoundException($"No such file: {path}");

            Console.Error.WriteLine($"Extracting entry [{path}] to [{outputPath}]");

            // 确保父目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            uint mode = entry.Mode & 0777;
            uint fileType = entry.Mode & CpioFileType.S_IFMT;

            switch (fileType)
            {
                case CpioFileType.S_IFDIR:
                    Directory.CreateDirectory(outputPath);
                    break;

                case CpioFileType.S_IFREG:
                    File.WriteAllBytes(outputPath, entry.Data);
                    break;

                case CpioFileType.S_IFLNK:
                    string linkTarget = Encoding.UTF8.GetString(entry.Data);
                    // 跨平台考虑，写入文本文件表示链接指向
                    File.WriteAllText(outputPath, $"Symlink to: {linkTarget}");
                    
                    break;

                case CpioFileType.S_IFBLK:
                case CpioFileType.S_IFCHR:
                    // 跨平台考虑，在Windows上没有直接对应的块设备/字符设备，写一个标记文件
                    File.WriteAllText(outputPath, $"Device file: major={entry.RdevMajor}, minor={entry.RdevMinor}");
                    break;

                default:
                    throw new NotSupportedException($"Unknown entry type: {fileType}");
            }
        }

        /// <summary>
        /// 提取全部或指定条目
        /// </summary>
        /// <param name="path">要提取的条目路径，为null则提取全部</param>
        /// <param name="outputPath">输出路径，为null时使用条目名作为路径</param>
        public void Extract(string path = null, string outputPath = null)
        {
            if (path != null && outputPath != null)
            {
                ExtractEntry(path, outputPath);
                return;
            }

            // 提取所有条目到当前目录
            foreach (var name in entries.Keys)
            {
                if (name == "." || name == "..")
                    continue;

                ExtractEntry(name, name);
            }
        }

        /// <summary>
        /// 检查路径是否存在
        /// </summary>
        /// <param name="path">要检查的路径</param>
        /// <returns>路径是否存在</returns>
        public bool Exists(string path)
        {
            return entries.ContainsKey(PathHelper.NormalizePath(path));
        }

        /// <summary>
        /// 添加文件到CPIO档案
        /// </summary>
        /// <param name="mode">文件权限模式</param>
        /// <param name="path">条目路径</param>
        /// <param name="filePath">要添加的文件路径</param>
        /// <exception cref="ArgumentException">路径格式错误时抛出</exception>
        public void Add(uint mode, string path, string filePath)
        {
            if (path.EndsWith("/"))
                throw new ArgumentException("Path cannot end with / for add");

            string normalizedPath = PathHelper.NormalizePath(path);

            byte[] content;
            uint rdevMajor = 0;
            uint rdevMinor = 0;

            FileAttributes attr = File.GetAttributes(filePath);

            // Windows不支持直接获取设备信息，所以我们只处理普通文件和目录
            if (!attr.HasFlag(FileAttributes.Directory))
            {
                // 处理为普通文件
                content = File.ReadAllBytes(filePath);
                mode |= CpioFileType.S_IFREG;
            }
            else
            {
                // 不应该到达这里 - 目录应使用 Mkdir 方法
                content = new byte[0];
                mode |= CpioFileType.S_IFDIR;
            }

            entries[normalizedPath] = new CpioEntry
            {
                Mode = mode,
                Uid = 0,
                Gid = 0,
                RdevMajor = rdevMajor,
                RdevMinor = rdevMinor,
                Data = content
            };

            Console.Error.WriteLine($"Add file [{path}] ({mode:o4})");
        }

        /// <summary>
        /// 在CPIO中创建目录
        /// </summary>
        /// <param name="mode">目录权限模式</param>
        /// <param name="dir">目录路径</param>
        public void Mkdir(uint mode, string dir)
        {
            string normalizedPath = PathHelper.NormalizePath(dir);

            entries[normalizedPath] = new CpioEntry
            {
                Mode = mode | CpioFileType.S_IFDIR,
                Uid = 0,
                Gid = 0,
                RdevMajor = 0,
                RdevMinor = 0,
                Data = new byte[0]
            };

            Console.Error.WriteLine($"Create directory [{dir}] ({mode:o4})");
        }

        /// <summary>
        /// 创建符号链接
        /// </summary>
        /// <param name="source">链接目标路径</param>
        /// <param name="destination">链接文件路径</param>
        public void CreateLink(string source, string destination)
        {
            string normalizedDest = PathHelper.NormalizePath(destination);
            byte[] sourcePath = Encoding.UTF8.GetBytes(PathHelper.NormalizePath(source));

            entries[normalizedDest] = new CpioEntry
            {
                Mode = CpioFileType.S_IFLNK,
                Uid = 0,
                Gid = 0,
                RdevMajor = 0,
                RdevMinor = 0,
                Data = sourcePath
            };

            Console.Error.WriteLine($"Create symlink [{destination}] -> [{source}]");
        }

        /// <summary>
        /// 移动/重命名条目
        /// </summary>
        /// <param name="from">源路径</param>
        /// <param name="to">目标路径</param>
        /// <exception cref="FileNotFoundException">源文件不存在时抛出</exception>
        public void Move(string from, string to)
        {
            string normalizedFrom = PathHelper.NormalizePath(from);
            string normalizedTo = PathHelper.NormalizePath(to);

            if (!entries.TryGetValue(normalizedFrom, out var entry))
                throw new FileNotFoundException($"No such entry: {from}");

            entries.Remove(normalizedFrom);
            entries[normalizedTo] = entry;

            Console.Error.WriteLine($"Move [{from}] -> [{to}]");
        }

        /// <summary>
        /// 列出CPIO内容
        /// </summary>
        /// <param name="path">要列出的路径，默认为根目录</param>
        /// <param name="recursive">是否递归列出</param>
        /// <returns>格式化的条目列表</returns>
        public IEnumerable<string> List(string path = "/", bool recursive = false)
        {
            string normalizedPath = PathHelper.NormalizePath(path);
            string prefix = string.IsNullOrEmpty(normalizedPath) ? "" : "/" + normalizedPath;
            List<string> results = new List<string>();

            foreach (var pair in entries)
            {
                string name = pair.Key;
                var entry = pair.Value;

                string fullPath = "/" + name;

                if (!fullPath.StartsWith(prefix))
                    continue;

                string relativePath = fullPath.Substring(prefix.Length);

                if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith("/"))
                    continue;

                if (!recursive && !string.IsNullOrEmpty(relativePath) &&
                    relativePath.Count(c => c == '/') > 1)
                    continue;

                string formattedEntry = $"{entry.FormatFileMode()}\t{name}";
                results.Add(formattedEntry);
            }

            return results;
        }
    }
}
