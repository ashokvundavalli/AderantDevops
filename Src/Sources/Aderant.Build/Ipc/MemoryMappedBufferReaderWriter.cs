using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Aderant.Build.Ipc {
    /// <summary>
    /// Reads and writes arbitrary data from a memory mapped file.
    /// </summary>
    public sealed class MemoryMappedBufferReaderWriter {
        private readonly BinaryFormatter formatter = new BinaryFormatter();
        private readonly MemoryMappedBuffer buffer;
        private bool bufferBroken;
        private object syncLock = new object();

        /// <summary>
        /// Gets the latest incoming message.
        /// </summary>
        public object IncomingMessage { get; private set; }

        internal MemoryMappedBufferReaderWriter(MemoryMappedBuffer buffer) {
            this.buffer = buffer;

            this.buffer.Disposing += OnDisposing;
        }

        public static string WriteData(object data) {
            if (!data.GetType().IsSerializable) {
                throw new ArgumentException("Type is not serializable.", nameof(data));
            }

            var name = Guid.NewGuid().ToString("D");

            var buffer = new MemoryMappedBuffer(name, MemoryMappedBuffer.DefaultCapacity);
            var readerWriter = new MemoryMappedBufferReaderWriter(buffer);
            readerWriter.Write(data);

            return name;
        }

        public static object Read(string name) {
            using (var buffer = new MemoryMappedBuffer(name, MemoryMappedBuffer.DefaultCapacity)) {
                var readerWriter = new MemoryMappedBufferReaderWriter(buffer);

                readerWriter.Read();

                return readerWriter.IncomingMessage;
            }
        }

        private void OnDisposing(object sender, EventArgs args) {
            lock (syncLock) {
                bufferBroken = true;
                buffer.Disposing -= OnDisposing;
            }
        }

        /// <summary>
        /// Writes the specified logging event to the underlying buffer.
        /// </summary>
        public void Write(object data)  {
            if (data == null) {
                return;
            }

            if (!data.GetType().IsSerializable) {
                throw new ArgumentException("Type is not serializable.", nameof(data));
            }

            lock (syncLock) {
                if (bufferBroken) {
                    return;
                }
               
                using (var stream = new MemoryStream()) {
                    formatter.Serialize(stream, data);

                    try {
                        buffer.Write(stream.ToArray());
                    } catch (ObjectDisposedException) {
                        bufferBroken = true;
                    }
                }
            }
        }

        private object Read(byte[] data) {

            lock (syncLock) {
                object rawEvent;

                using (MemoryStream stream = new MemoryStream(data)) {
                    try {
                        rawEvent = formatter.Deserialize(stream);
                    } catch (SerializationException) {
                        return null;
                    } catch (ArgumentNullException) {
                        // Corrupted streams will cause this exception as well
                        return null;
                    }
                }

                return rawEvent;
            }
        }

        /// <summary>
        /// Reads the next available message from the underlying buffer.
        /// The message is available in <see cref="IncomingMessage"/>
        /// </summary>
        public bool Read() {
            if (bufferBroken) {
                IncomingMessage = null;
                return false;
            }

            try {
                byte[] bytes = buffer.Read();

                if (bytes.Length == 0) {
                    IncomingMessage = null;
                    return false;
                }

                IncomingMessage = Read(bytes);

                if (IncomingMessage == null) {
                    return false;
                }

                return true;
            } catch (ObjectDisposedException) {
                IncomingMessage = null;
                return false;
            }
        }
    }
}