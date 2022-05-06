namespace TutoProxy.Core.Models {
    public class TransferRequestModel : TransferBase {
        public DataRequestModel Payload { get; private set; }

        public TransferRequestModel(DataRequestModel payload)
            : base(Guid.NewGuid().ToString(), DateTime.Now) {
            Payload = payload;
        }

        public override string ToString() {
            return $"{base.ToString()}, Payload: '{Payload}'";
        }
    }
}
