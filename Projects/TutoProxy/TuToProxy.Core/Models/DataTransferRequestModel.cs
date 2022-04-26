namespace TutoProxy.Core.Models {
    public class DataTransferRequestModel : DataTransferBase {
        public string? Payload { get; set; }

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
