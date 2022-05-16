namespace TutoProxy.Core.Models {
    public class TransferRequestModel : TransferBase {
        public DataRequestModel Payload { get; private set; }

        public TransferRequestModel(DataRequestModel payload, string id, DateTime created)
            : base(id, created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
