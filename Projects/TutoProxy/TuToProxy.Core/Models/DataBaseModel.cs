namespace TuToProxy.Core.Models {
    public abstract class DataBaseModel {
        public string Data { get; set; } = string.Empty;

        public override string ToString() {
            return $"{Data}";
        }
    }
}
