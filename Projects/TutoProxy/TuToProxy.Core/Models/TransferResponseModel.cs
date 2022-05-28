namespace TutoProxy.Core.Models {
    public class TransferUdpResponseModel : TransferBase {
        public UdpDataResponseModel Payload { get; set; }

        public TransferUdpResponseModel() : base(string.Empty, default) {
            Payload = new();
        }

        public TransferUdpResponseModel(TransferUdpRequestModel requestModel, UdpDataResponseModel payload)
            : base(requestModel.Id, requestModel.Created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"udp {base.ToString()}, Payload: '{Payload}'";
        }
    }

    public class TransferTcpResponseModel : TransferBase {
        public TcpDataResponseModel Payload { get; set; }

        public TransferTcpResponseModel() : base(string.Empty, default) {
            Payload = new();
        }

        public TransferTcpResponseModel(TransferTcpRequestModel requestModel, TcpDataResponseModel payload)
            : base(requestModel.Id, requestModel.Created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"tcp {base.ToString()}, Payload: '{Payload}'";
        }
    }
}
