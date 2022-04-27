namespace TutoProxy.Core.Models {
    public class TransferResponseModel : TransferBase {
        public DataResponseModel Payload { get; set; } = new();


        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
