namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public int Port { get; set; }
        public int OriginPort { get; set; }
        public byte[] Data { get; set; }

        public DataBaseModel(int port, int originPort, byte[] data) {
            Port = port;
            OriginPort = originPort;
            Data = data;
        }

        public override string ToString() {
            return $"port:{Port}, o-port:{OriginPort}, {Data.Length} b";
        }
    }
}
