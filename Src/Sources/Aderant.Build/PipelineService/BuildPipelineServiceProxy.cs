using System;
using System.Globalization;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using ProtoBuf.ServiceModel;

namespace Aderant.Build.PipelineService {

    /// <summary>
    /// Technology specific (WCF) proxy for the build data service
    /// </summary>
    internal class BuildPipelineServiceProxy : ClientBase<IBuildPipelineService> {

        static BuildPipelineServiceProxy() {
            CacheSetting = CacheSetting.AlwaysOn;
        }

        public BuildPipelineServiceProxy(Binding binding, EndpointAddress address)
            : base(binding, address) {
            Endpoint.Behaviors.Add(new ProtoEndpointBehavior());
        }

        /// <summary>
        /// Exposes the underlying channel contract.
        /// </summary>
        public IBuildPipelineService ChannelContract {
            get { return base.Channel; }
        }
    }

    internal static class ExceptionConverter {

        public static Exception ConvertException(FaultException faultEx) {
            if (faultEx.Code == null || faultEx.Code.Name == null) {
                return new BuildPlatformException(faultEx.Message, faultEx);
            }

            return ConvertException(faultEx.Code.Name, faultEx.Message, faultEx);
        }

        private static Exception ConvertException(string exceptionType, string message, Exception innerException) {
            try {
                string typeName2 = "System." + exceptionType;
                return (Exception)Activator.CreateInstance(
                    typeof(Exception),
                    typeName2,
                    true,
                    BindingFlags.Default,
                    null,
                    new object[] {
                        message,
                        innerException
                    },
                    CultureInfo.InvariantCulture);
            } catch (Exception) {
                return new BuildPlatformException(message, innerException);
            }
        }
    }

    [Serializable]
    public class BuildPlatformException : Exception {
        public BuildPlatformException(string message)
            : base(message) {
        }

        public BuildPlatformException(string message, Exception innerException)
            : base(message, innerException) {
        }
    }

}