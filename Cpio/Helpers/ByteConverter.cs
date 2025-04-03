using System.Runtime.InteropServices;
using System.Text;

namespace Cpio.Helpers
{
    /// <summary>
    /// 字节处理辅助类
    /// </summary>
    internal static class ByteConverter
    {
        /// <summary>
        /// 将字节数组转换为结构体
        /// </summary>
        /// <typeparam name="T">目标结构体类型</typeparam>
        /// <param name="bytes">源字节数组</param>
        /// <param name="offset">起始偏移量</param>
        /// <param name="size">结构体大小</param>
        /// <returns>转换后的结构体</returns>
        public static T BytesToStruct<T>(byte[] bytes, int offset, int size) where T : struct
        {
            byte[] temp = new byte[size];
            Buffer.BlockCopy(bytes, offset, temp, 0, size);

            GCHandle handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// 解析CPIO头中的十六进制字段
        /// </summary>
        /// <param name="field">十六进制字符的字节数组</param>
        /// <returns>解析后的十进制值</returns>
        public static uint ParseHexField(byte[] field)
        {
            string hexString = Encoding.ASCII.GetString(field);
            return Convert.ToUInt32(hexString, 16);
        }

        /// <summary>
        /// 对齐到4字节边界
        /// </summary>
        /// <param name="x">输入值</param>
        /// <returns>对齐后的值</returns>
        public static int Align4(int x)
        {
            return (x + 3) & ~3;
        }

        /// <summary>
        /// 在字节数组中查找模式
        /// </summary>
        /// <param name="source">源数组</param>
        /// <param name="pattern">要查找的模式</param>
        /// <param name="startIndex">起始索引</param>
        /// <returns>找到的位置，未找到则返回-1</returns>
        public static int FindBytes(byte[] source, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= source.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
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
    }
}
