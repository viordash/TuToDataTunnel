namespace TutoProxy.Core.Models {
    public class TransferRequestModel : TransferBase {
        public DataRequestModel Payload { get; set; } = new();

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
