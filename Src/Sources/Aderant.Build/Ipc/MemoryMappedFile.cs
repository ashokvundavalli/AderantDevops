using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text.RegularExpressions;
using System.Threading;

namespace Aderant.Build.Ipc {
    /// <summary>
    /// A thread safe IPC channel backed shared memory.
    /// </summary>
    internal class MemoryMappedFile : IDisposable {
        private static List<object> gcKeepAlive = new List<object>();

        private System.IO.MemoryMappedFiles.MemoryMappedFile buffer;
        private Semaphore bufferFreeForWriting;

        private object syncLock = new object();
        private MemoryMappedViewAccessor view;

        public MemoryMappedFile(string bufferName, Int64 capacity) {
            if (!TryOpenExisting(bufferName, out buffer)) {
                buffer = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen(bufferName, capacity, MemoryMappedFileAccess.ReadWrite);
                gcKeepAlive.Add(buffer);
            }

            view = buffer.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

            string name = GetBufferNameWithoutSession(bufferName);

            // Concurrency control, all writers will block on this handle
            bool createdNew;
            bufferFreeForWriting = new Semaphore(1, 1, name + "_BUFFER_READY", out createdNew);
        }

        internal static long DefaultCapacity {
            get { return 4194304; }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public event EventHandler<EventArgs> Disposing;

        private string GetBufferNameWithoutSession(string bufferName) {
            return Regex.Replace(bufferName, @"(Local\\|Global\\)", string.Empty);
        }

        public byte[] Read() {
            bufferFreeForWriting.WaitOne();

            byte[] result = ReadBuffer(view);

            bufferFreeForWriting.Release();

            if (Array.TrueForAll(result, b => b == '\0')) {
                return null;
            }

            return result;
        }

        public void Write(byte[] data) {
            if (data.Length > view.Capacity) {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Payload {0} to be written exceeds view capacity {1}", data.Length, view.Capacity));
            }

            bufferFreeForWriting.WaitOne();

            lock (syncLock) {
                WriteBuffer(data);
            }

            // Signal readers the data is available
            bufferFreeForWriting.Release();
        }

        protected virtual void WriteBuffer(byte[] data) {
            if (data.Length < view.Capacity) {
                // Grow the payload so we overwrite all data in the buffer. 
                // This helps the read stream so it doesn't have to deal with trailing junk from previous writes.
                Array.Resize(ref data, (int)view.Capacity);
            }

            view.WriteArray(0, data, 0, (int)view.Capacity);
        }

        public static byte[] ReadBuffer(UnmanagedMemoryAccessor memoryAccessor) {
            var result = new byte[memoryAccessor.Capacity];

            try {
                if (memoryAccessor.CanRead) {
                    memoryAccessor.ReadArray(0, result, 0, result.Length);
                    return result;
                }
            } catch (ObjectDisposedException) {
                // We don't know when the writer might dispose the file so guard for the file going away at any moment
            }

            return Array.Empty<byte>();
        }

        protected virtual void OnDisposing() {
            Disposing?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) {
                return;
            }

            OnDisposing();

            if (view != null) {
                try {
                    view.Flush();
                    view.Dispose();
                } catch (ObjectDisposedException) {

                }
            }

            if (buffer != null) {
                try {
                    buffer.Dispose();
                } catch (ObjectDisposedException) {

                }
            }
        }

        private static bool TryOpenExisting(string mapName, out System.IO.MemoryMappedFiles.MemoryMappedFile memoryMappedFile) {
            bool result = false;
            System.IO.MemoryMappedFiles.MemoryMappedFile mmf = null;
            try {
                mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(mapName);
            } catch {
                mmf = null;
            } finally {
                result = mmf != null;
                memoryMappedFile = mmf;
            }

            return result;
        }
    }
}
