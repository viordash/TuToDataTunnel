namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public override string ToString() {
            return $"{Data}";
        }
    }
}
