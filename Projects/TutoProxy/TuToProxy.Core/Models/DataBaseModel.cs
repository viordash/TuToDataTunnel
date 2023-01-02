using MessagePack;
using MessagePack.Formatters;
using static TuToProxy.Core.Models.DataBaseModel;

namespace TuToProxy.Core.Models {
    [MessagePackFormatter(typeof(SocketAddressModelFormatter))]
    public class SocketAddressModel {
        #region inner classes
        public class SocketAddressModelFormatter : IMessagePackFormatter<SocketAddressModel> {
            static IFormatterResolver resolver = MessagePack.Resolvers.BuiltinResolver.Instance;
            static IMessagePackFormatter<int> intFormatter = resolver.GetFormatter<int>();

            public void Serialize(ref MessagePackWriter writer, SocketAddressModel value, MessagePackSerializerOptions options) {
                intFormatter.Serialize(ref writer, value.Port, options);
                intFormatter.Serialize(ref writer, value.OriginPort, options);
            }

            public SocketAddressModel Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
                var port = intFormatter.Deserialize(ref reader, options);
                var originPort = intFormatter.Deserialize(ref reader, options);
                return new SocketAddressModel() { Port = port, OriginPort = originPort };
            }
        }
        #endregion

        [Key(0)]
        public int Port { get; set; }
        [Key(1)]
        public int OriginPort { get; set; }

        public override string ToString() {
            return $"port:{Port}, o-port:{OriginPort}";
        }
    }

    public abstract class DataBaseModel : SocketAddressModel {
        #region inner classes
        public class DataBaseModelFormatter<T> : IMessagePackFormatter<T> where T : DataBaseModel, new() {
            static IFormatterResolver resolver = MessagePack.Resolvers.BuiltinResolver.Instance;
            static IMessagePackFormatter<int> intFormatter = resolver.GetFormatter<int>();
            static IMessagePackFormatter<ReadOnlyMemory<byte>> dataFormatter = resolver.GetFormatter<ReadOnlyMemory<byte>>();

            public void Serialize(ref MessagePackWriter writer, T value, MessagePackSerializerOptions options) {
                intFormatter.Serialize(ref writer, value.Port, options);
                intFormatter.Serialize(ref writer, value.OriginPort, options);
                dataFormatter.Serialize(ref writer, value.Data, options);
            }

            public T Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
                var port = intFormatter.Deserialize(ref reader, options);
                var originPort = intFormatter.Deserialize(ref reader, options);
                var data = dataFormatter.Deserialize(ref reader, options);
                return new T() { Port = port, OriginPort = originPort, Data = data };
            }
        }
        #endregion

        [Key(2)]
        public ReadOnlyMemory<byte> Data { get; set; } = new byte[0];

        public override string ToString() {
            return $"{base.ToString()}, {Data.Length} b";
        }
    }
}
