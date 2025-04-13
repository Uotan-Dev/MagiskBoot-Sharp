using System.IO.MemoryMappedFiles;
using System.Text;

namespace HexPatch
{
    public class Patch
    {
        /// <summary>
        /// 从缓冲区中移除与验证启动相关的模式
        /// </summary>
        public static int PatchVerity(byte[] buf)
        {
            return RemovePattern(buf, MatchVerityPattern);
        }

        /// <summary>
        /// 从缓冲区中移除与加密相关的模式
        /// </summary>
        public static int PatchEncryption(byte[] buf)
        {
            return RemovePattern(buf, MatchEncryptionPattern);
        }

        /// <summary>
        /// 在文件中查找并替换十六进制模式
        /// </summary>
        public static bool HexPatch(byte[] file, byte[] from, byte[] to)
        {
            try
            {
                string filePath = Encoding.UTF8.GetString(file).TrimEnd('\0');
                string fromHex = Encoding.UTF8.GetString(from).TrimEnd('\0');
                string toHex = Encoding.UTF8.GetString(to).TrimEnd('\0');

                byte[] pattern = HexToByte(fromHex);
                byte[] patch = HexToByte(toHex);

                using (var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite))
                {
                    using (var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite))
                    {
                        byte[] buffer = new byte[accessor.Capacity];
                        accessor.ReadArray(0, buffer, 0, buffer.Length);

                        List<int> patched = new List<int>();
                        for (int i = 0; i <= buffer.Length - pattern.Length; i++)
                        {
                            bool match = true;
                            for (int j = 0; j < pattern.Length; j++)
                            {
                                if (buffer[i + j] != pattern[j])
                                {
                                    match = false;
                                    break;
                                }
                            }

                            if (match)
                            {
                                patched.Add(i);
                                for (int j = 0; j < Math.Min(pattern.Length, patch.Length); j++)
                                {
                                    accessor.Write(i + j, patch[j]);
                                }
                                Console.Error.WriteLine($"Patch @ 0x{i:X10} [{fromHex}] -> [{toHex}]");
                            }
                        }

                        return patched.Count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during hex patching: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将十六进制字符串转换为字节数组
        /// </summary>
        public static byte[] HexToByte(string hex)
        {
            byte[] hexBytes = Encoding.ASCII.GetBytes(hex);
            List<byte> result = new List<byte>(hexBytes.Length / 2);

            for (int i = 0; i < hexBytes.Length - 1; i += 2)
            {
                byte high = (byte)(char.ToUpper((char)hexBytes[i]) - '0');
                byte low = (byte)(char.ToUpper((char)hexBytes[i + 1]) - '0');

                high = (byte)(high > 9 ? high - 7 : high);
                low = (byte)(low > 9 ? low - 7 : low);

                result.Add((byte)((high << 4) | low));
            }

            return result.ToArray();
        }

        /// <summary>
        /// 从缓冲区中移除匹配的模式
        /// </summary>
        private static int RemovePattern(byte[] buf, Func<byte[], int?> patternMatcher)
        {
            int write = 0;
            int read = 0;
            int size = buf.Length;

            while (read < buf.Length)
            {
                int? matchLength = patternMatcher(buf.AsSpan(read).ToArray());
                if (matchLength.HasValue)
                {
                    string skipped = Encoding.ASCII.GetString(buf, read, matchLength.Value);
                    Console.Error.WriteLine($"Remove pattern [{skipped}]");
                    size -= matchLength.Value;
                    read += matchLength.Value;
                }
                else
                {
                    buf[write] = buf[read];
                    write++;
                    read++;
                }
            }

            // 用零填充剩余空间
            for (int i = write; i < buf.Length; i++)
            {
                buf[i] = 0;
            }

            return size;
        }

        /// <summary>
        /// 匹配验证启动相关的模式
        /// </summary>
        private static int? MatchVerityPattern(byte[] buffer)
        {
            return MatchPatterns(buffer,
            [
                "verifyatboot",
                "verify",
                "avb_keys",
                "avb",
                "support_scfs",
                "fsverity"
            ]);
        }

        /// <summary>
        /// 匹配加密相关的模式
        /// </summary>
        private static int? MatchEncryptionPattern(byte[] buffer)
        {
            return MatchPatterns(buffer,
            [
                "forceencrypt",
                "forcefdeorfbe",
                "fileencryption"
            ]);
        }

        /// <summary>
        /// 实现 match_patterns! 宏的功能
        /// </summary>
        private static int? MatchPatterns(byte[] buffer, string[] patterns)
        {
            if (buffer.Length == 0)
            {
                return null;
            }

            int offset = buffer[0] == ',' ? 1 : 0;
            if (offset >= buffer.Length)
            {
                return null;
            }

            byte[] b = buffer.AsSpan(offset).ToArray();
            if (b.Length == 0)
            {
                return null;
            }

            bool found = false;
            string matchedPattern = null;

            foreach (string pattern in patterns)
            {
                byte[] patternBytes = Encoding.ASCII.GetBytes(pattern);
                if (b.Length >= patternBytes.Length &&
                    b.AsSpan(0, patternBytes.Length).SequenceEqual(patternBytes))
                {
                    offset += patternBytes.Length;
                    found = true;
                    matchedPattern = pattern;
                    break;
                }
            }

            if (!found)
            {
                return null;
            }

            // 如果后面跟着 '='，需要处理等号后面的内容
            if (offset < buffer.Length && buffer[offset] == '=')
            {
                offset++;
                while (offset < buffer.Length)
                {
                    byte c = buffer[offset];
                    if (c == ' ' || c == '\n' || c == '\0')
                    {
                        break;
                    }
                    offset++;
                }
            }

            return offset;
        }
    }
}
