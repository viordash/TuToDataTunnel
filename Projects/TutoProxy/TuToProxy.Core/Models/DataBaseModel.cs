namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public int Port { get; set; }
        public byte[] Data { get; set; }

        public DataBaseModel(int port, byte[] data) {
            Port = port;
            Data = data;
        }

        public override string ToString() {
            return $"port:{Port}, {Data}";
        }
    }
}
