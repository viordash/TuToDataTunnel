namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public int Port { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public override string ToString() {
            return $"port:{Port}, {Data}";
        }
    }
}
