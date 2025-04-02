using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cpio.Models
{
    /// <summary>
    /// CPIO 归档中的单个条目
    /// </summary>
    public class CpioEntry
    {
        /// <summary>文件模式(包括类型和权限)</summary>
        public uint Mode { get; set; }

        /// <summary>用户ID</summary>
        public uint Uid { get; set; }

        /// <summary>组ID</summary>
        public uint Gid { get; set; }

        /// <summary>设备主设备号(对设备文件有效)</summary>
        public uint RdevMajor { get; set; }

        /// <summary>设备次设备号(对设备文件有效)</summary>
        public uint RdevMinor { get; set; }

        /// <summary>文件内容字节数据</summary>
        public byte[] Data { get; set; }
    }
}
