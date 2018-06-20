using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Aderant.Build.Ipc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTest.Build.Ipc {
    [TestClass]
    public class MemoryMappedBufferTests {

        [TestMethod]
        public void Can_write_to_shared_memory_without_exception() {
            using (var block = new MemoryMappedFile(@"Local\" + Guid.NewGuid(), 4096)) {
                block.Write(new byte[] { 0x00, 0x01, 0x02 });
            }
        }

        [TestMethod]
        public void Can_read_and_write_to_shared_memory_without_exception() {
            const int capacity = 4096;

            byte[] input;
            byte[] data;

            using (var block = new MemoryMappedFile(@"Local\" + Guid.NewGuid(), capacity)) {
                input = new byte[] { 0x00, 0x01, 0x02 };
                block.Write(input);

                data = block.Read();
            }

            Assert.AreEqual(capacity, data.Length);
            Assert.IsTrue(Enumerable.SequenceEqual(input, data.Take(3)) /* We want the first 3 elements as the block is always set to capacity */);
        }

        [TestMethod]
        public void Can_read_and_write_from_threads() {
            var block = new MemoryMappedFile(@"Local\" + Guid.NewGuid(), 4096);

            bool[] run = { true };

            var writer = new Thread(() => {
                while (run[0]) {
                    block.Write(new byte[] { 0x4d, 0x69, 0x63, 0x68, 0x61, 0x65, 0x6c, 0x20, 0x42, 0x61, 0x6b, 0x65, 0x72 });
                }
            });

            List<Byte[]> recievedData = new List<byte[]>();

            var reader = new Thread(() => {
                while (run[0]) {
                    byte[] read = block.Read();
                    recievedData.Add(read);
                }
            });

            reader.Start();
            writer.Start();

            Thread.Sleep(TimeSpan.FromSeconds(2));

            run[0] = false;

            writer.Join(100);
            reader.Join(100);

            Assert.AreNotEqual(0, recievedData.Count);
            Assert.IsTrue(recievedData.Select(item => Encoding.ASCII.GetString(item).Trim('\0')).All(str => string.Equals(str, "Michael Baker")));

            block.Dispose();
        }
    }
}
