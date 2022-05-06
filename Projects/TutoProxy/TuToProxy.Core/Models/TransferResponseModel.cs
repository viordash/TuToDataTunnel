namespace TutoProxy.Core.Models {
    public class TransferResponseModel : TransferBase {
        public DataResponseModel Payload { get; private set; }

        public TransferResponseModel(TransferRequestModel requestModel, DataResponseModel payload)
            : base(requestModel.Id, requestModel.DateTime) {
            Payload = payload;
        }

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
