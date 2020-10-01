using System.IO;
using ProtoBuf;

namespace UnitTest.Build.Serialization {
    public class SerializationBase {
        internal static T RoundTrip<T>(T artifact) {
            return ProtoDeserialize<T>(ProtoSerialize(artifact));
        }

        private static T ProtoDeserialize<T>(byte[] data) {
            using (var stream = new MemoryStream(data)) {
                return Serializer.Deserialize<T>(stream);
            }
        }

        private static byte[] ProtoSerialize<T>(T graph) {
            using (var stream = new MemoryStream()) {
                Serializer.Serialize(stream, graph);
                stream.Position = 0;
                return stream.ToArray();
            }
        }
    }
}
