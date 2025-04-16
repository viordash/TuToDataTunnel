using MessagePack;
using MessagePack.Formatters;

namespace TuToProxy.Core.Models {
    [MessagePackFormatter(typeof(SocketAddressModelFormatter))]
    public class SocketAddressModel {
        #region inner classes
        public class SocketAddressModelFormatter : IMessagePackFormatter<SocketAddressModel?> {
            static readonly MessagePack.Resolvers.BuiltinResolver resolver = MessagePack.Resolvers.BuiltinResolver.Instance;
            static readonly IMessagePackFormatter<int>? intFormatter = resolver.GetFormatter<int>();

            public void Serialize(ref MessagePackWriter writer, SocketAddressModel? value, MessagePackSerializerOptions options) {
                if(intFormatter is null || value is null) {
                    return;
                }
                intFormatter.Serialize(ref writer, value.Port, options);
                intFormatter.Serialize(ref writer, value.OriginPort, options);
            }

            public SocketAddressModel? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
                if(intFormatter is null) {
                    return null;
                }
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
        public class DataBaseModelFormatter<T> : IMessagePackFormatter<T?> where T : DataBaseModel, new() {
            static readonly MessagePack.Resolvers.BuiltinResolver resolver = MessagePack.Resolvers.BuiltinResolver.Instance;
            static readonly IMessagePackFormatter<int>? intFormatter = resolver.GetFormatter<int>();
            static readonly IMessagePackFormatter<ReadOnlyMemory<byte>>? dataFormatter = resolver.GetFormatter<ReadOnlyMemory<byte>>();

            public void Serialize(ref MessagePackWriter writer, T? value, MessagePackSerializerOptions options) {
                if(intFormatter is null || dataFormatter is null || value is null) {
                    return;
                }
                intFormatter.Serialize(ref writer, value.Port, options);
                intFormatter.Serialize(ref writer, value.OriginPort, options);
                dataFormatter.Serialize(ref writer, value.Data, options);
            }

            public T? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) {
                if(intFormatter is null || dataFormatter is null) {
                    return null;
                }
                var port = intFormatter.Deserialize(ref reader, options);
                var originPort = intFormatter.Deserialize(ref reader, options);
                var data = dataFormatter.Deserialize(ref reader, options);
                return new T() { Port = port, OriginPort = originPort, Data = data };
            }
        }
        #endregion

        [Key(2)]
        public ReadOnlyMemory<byte> Data { get; set; } = Array.Empty<byte>();

        public override string ToString() {
            return $"{base.ToString()}, {Data.Length} b";
        }
    }
}
