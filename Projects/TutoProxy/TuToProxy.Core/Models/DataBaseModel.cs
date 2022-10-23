namespace TuToProxy.Core.Models {
    public abstract class SocketAddressModel {
        public int Port { get; set; }
        public int OriginPort { get; set; }

        public SocketAddressModel(int port, int originPort) {
            Port = port;
            OriginPort = originPort;
        }

        public override string ToString() {
            return $"port:{Port}, o-port:{OriginPort}";
        }
    }

    public abstract class DataBaseModel : SocketAddressModel {
        public byte[] Data { get; set; }

        public DataBaseModel(int port, int originPort, byte[] data) : base(port, originPort) {
            Data = data;
        }

        public override string ToString() {
            return $"{base.ToString()}, {Data.Length} b";
        }
    }
}
