using MessagePack;

namespace TuToProxy.Core.Models {
    [MessagePackObject]
    public class SocketAddressModel {
        [Key(0)]
        public int Port { get; set; }
        [Key(1)]
        public int OriginPort { get; set; }

        public override string ToString() {
            return $"port:{Port}, o-port:{OriginPort}";
        }
    }

    public abstract class DataBaseModel : SocketAddressModel {
        [Key(3)]
        public byte[] Data { get; set; } = new byte[0];

        public override string ToString() {
            return $"{base.ToString()}, {Data?.Length} b";
        }
    }
}
