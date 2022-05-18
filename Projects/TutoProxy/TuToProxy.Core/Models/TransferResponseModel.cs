namespace TutoProxy.Core.Models {
    public class TransferResponseModel : TransferBase {
        public DataResponseModel Payload { get; set; }

        public TransferResponseModel() : base(string.Empty, default) {
            Payload = new();
        }

        public TransferResponseModel(TransferRequestModel requestModel, DataResponseModel payload)
            : base(requestModel.Id, requestModel.Created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
