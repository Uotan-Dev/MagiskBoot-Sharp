
using DeviceTreeNode.Core;

namespace DeviceTreeNodeTests
{
    [TestClass]
    public class FdtParsingTests
    {
        // 从嵌入资源或文件加载测试DTB
        private static byte[] LoadTestDtb()
        {
            // 实际项目中可以从项目资源加载
            // 这里为了示例，我们直接从文件加载
            return File.ReadAllBytes("test.dtb");
        }

        [TestMethod]
        public void TestFdtHeaderParsing()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            Assert.IsNotNull(fdt.Header);
            Assert.IsTrue(fdt.Header.ValidMagic);
            Assert.IsTrue(fdt.Header.TotalSize > 0);
            Assert.IsTrue(fdt.Header.Version >= 16); // FDT版本通常为16或更高
        }

        [TestMethod]
        public void TestBigEndianValues()
        {
            // 测试32位大端序值解析
            byte[] bytes32 = new byte[] { 0x12, 0x34, 0x56, 0x78 };
            var value32 = BigEndianU32.FromBytes(bytes32);

            Assert.IsTrue(value32.HasValue);
            Assert.AreEqual(0x12345678u, value32.Value.Value);

            // 测试64位大端序值解析
            byte[] bytes64 = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 };
            var value64 = BigEndianU64.FromBytes(bytes64);

            Assert.IsTrue(value64.HasValue);
            Assert.AreEqual(0x123456789ABCDEF0ul, value64.Value.Value);
        }

        [TestMethod]
        public void TestFdtDataOperations()
        {
            // 创建测试数据
            byte[] testData = new byte[] {
                0x01, 0x02, 0x03, 0x04,  // BigEndianU32 = 0x01020304
                0x05, 0x06, 0x07, 0x08,  // BigEndianU32 = 0x05060708
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 // BigEndianU64
            };

            var fdtData = new FdtData(testData);

            // 测试读取32位整数
            var val1 = fdtData.ReadUInt32();
            Assert.AreEqual(0x01020304u, val1.Value.Value);

            // 测试预览32位整数
            var peek = fdtData.PeekUInt32();
            Assert.AreEqual(0x05060708u, peek.Value.Value);

            // 测试读取不会改变位置
            Assert.AreEqual(0x05060708u, fdtData.ReadUInt32().Value.Value);

            // 测试读取64位整数
            var val64 = fdtData.ReadUInt64();
            Assert.AreEqual(0x090A0B0C0D0E0F10ul, val64.Value.Value);

            // 测试结束标志
            Assert.IsTrue(fdtData.IsEmpty());
        }

        [TestMethod]
        public void TestFindNode()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            // 查找根节点
            var root = fdt.Root.Node;
            Assert.IsNotNull(root);
            Assert.AreEqual("", root.Name);

            // 查找/chosen节点
            var chosen = fdt.FindNode("/chosen");
            Assert.IsNotNull(chosen);
            Assert.AreEqual("chosen", chosen.Name);

            // 嵌套节点查找，如/soc/uart
            var soc = fdt.FindNode("/soc");
            if (soc != null) // 可能在某些测试DTB中不存在
            {
                var uarts = soc.Children().Where(n => n.Name.StartsWith("uart@"));
                Assert.IsTrue(uarts.Any());
            }
        }

        [TestMethod]
        public void TestNodeProperties()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            // 获取型号属性
            var model = fdt.Root.Model;
            Assert.IsFalse(string.IsNullOrEmpty(model));

            // 测试兼容性属性
            var compatible = fdt.Root.Compatible;
            Assert.IsTrue(compatible.Length > 0);

            // 测试常见属性解析
            var chosen = fdt.Chosen;
            if (chosen != null && chosen.Bootargs != null)
            {
                Assert.IsTrue(chosen.Bootargs.Length > 0); // 可能在某些测试DTB中为[00]，false是正常现象
            }
        }

        [TestMethod]
        public void TestMemoryRegions()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            // 测试内存区域解析
            var memory = fdt.Memory;
            if (memory != null)
            {
                var regions = memory.Regions.ToList();
                Assert.IsTrue(regions.Count > 0);

                // 测试第一个区域
                var region = regions[0];
                Assert.IsNotNull(region);
                Assert.IsTrue(region.StartingAddress.ToInt64() > 0);
                Assert.IsTrue(region.Size.HasValue);
                Assert.IsTrue(region.Size.Value > 0);

                // 测试内存大小计算
                Assert.IsTrue(memory.TotalSize > 0);
            }
        }

        [TestMethod]
        public void TestCpuNodes()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            var cpus = fdt.Cpus.ToList();
            if (cpus.Count > 0)
            {
                // 测试CPU属性
                var firstCpu = cpus[0];
                Assert.IsNotNull(firstCpu);

                // 检查CPU ID
                var ids = firstCpu.Ids;
                Assert.IsTrue(ids.Length > 0);

                // CPU兼容性
                Assert.IsTrue(firstCpu.Compatible.Length > 0);
            }
        }

        [TestMethod]
        public void TestCellSizes()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            // 根节点通常定义#address-cells和#size-cells
            var rootCellSizes = fdt.Root.CellSizes;
            Assert.IsTrue(rootCellSizes.AddressCells >= 1);
            Assert.IsTrue(rootCellSizes.SizeCells >= 0);
        }

        [TestMethod]
        public void TestMemoryReservations()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            // 获取内存保留区域
            var reservations = fdt.MemoryReservations.ToList();

            // 不是所有DTB都有保留区域
            if (reservations.Count > 0)
            {
                var reservation = reservations[0];
                Assert.IsTrue(reservation.Size > 0);
                Assert.IsTrue(reservation.Address.ToInt64() >= 0);
            }
        }

        [TestMethod]
        public void TestCStringParsing()
        {
            // NULL终止的字符串测试
            byte[] testString = new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t', 0 };
            var cstr = CString.FromBytes(testString);

            Assert.IsNotNull(cstr);
            Assert.AreEqual(4, cstr.Length);
            Assert.AreEqual("test", cstr.AsString());

            // 没有NULL终止符的情况
            byte[] invalidString = new byte[] { (byte)'t', (byte)'e', (byte)'s', (byte)'t' };
            var invalidCstr = CString.FromBytes(invalidString);

            Assert.IsNull(invalidCstr);
        }

        [TestMethod]
        public void TestAliasesResolution()
        {
            var data = LoadTestDtb();
            var fdt = new Fdt(data);

            var aliases = fdt.Aliases;
            if (aliases != null)
            {
                // 获取所有别名
                var allAliases = aliases.GetAllAliases();

                // 如果有别名，尝试解析第一个
                if (allAliases.Count > 0)
                {
                    var firstAlias = allAliases.Keys.First();
                    var node = aliases.ResolveAlias(firstAlias);

                    Assert.IsNotNull(node);
                    Assert.IsTrue(node.Name.Length > 0);
                }
            }
        }
    }
}
