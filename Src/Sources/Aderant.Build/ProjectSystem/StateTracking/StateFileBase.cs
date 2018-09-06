using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using ProtoBuf;

namespace Aderant.Build.ProjectSystem.StateTracking {
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    [ProtoInclude(100, typeof(BuildStateFile))]
    [DataContract]
    public class StateFileBase {

        internal const byte CurrentSerializationVersion = 2;

        // Version this instance is serialized with.
        [DataMember]
        internal byte serializedVersion = CurrentSerializationVersion;

        internal static T DeserializeCache<T>(Stream stream) where T : StateFileBase {
            var serializer = CreateSerializer(typeof(T));

            object readObject = serializer.ReadObject(stream);

            T stateFile = readObject as T;

            if (stateFile != null && stateFile.serializedVersion != CurrentSerializationVersion) {
                return null;
            }

            return stateFile;
        }

        private static DataContractJsonSerializer CreateSerializer(Type type) {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(
                type,
                new DataContractJsonSerializerSettings {
                    UseSimpleDictionaryFormat = true
                });
            return ser;
        }

        /// <summary>
        /// Writes the contents of this object out.
        /// </summary>
        /// <param name="stream"></param>
        internal virtual void Serialize(Stream stream) {
            var ser = CreateSerializer(GetType());
            ser.WriteObject(stream, this);
        }
    }
}
