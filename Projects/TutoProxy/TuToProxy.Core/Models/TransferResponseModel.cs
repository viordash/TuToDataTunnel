namespace TutoProxy.Core.Models {
    public class TransferResponseModel : TransferBase {
        public DataResponseModel? Payload { get; set; }


        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
