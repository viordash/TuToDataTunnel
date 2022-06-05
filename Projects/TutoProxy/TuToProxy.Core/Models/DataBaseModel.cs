namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public int Port { get; set; }
        public int RemotePort { get; set; }
        public byte[] Data { get; set; }

        public DataBaseModel(int port, int remotePort, byte[] data) {
            Port = port;
            RemotePort = remotePort;
            Data = data;
        }

        public override string ToString() {
            return $"port:{Port}, {Data}";
        }
    }
}
