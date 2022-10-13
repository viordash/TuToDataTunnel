namespace TuToProxy.Core.Models {
    public class TransferUdpRequestModel : TransferBase {
        public UdpDataRequestModel Payload { get; set; }

        public TransferUdpRequestModel(UdpDataRequestModel payload, string id, DateTime created)
            : base(id, created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"udp {base.ToString()}, Payload: '{Payload}'";
        }
    }

    public class TransferTcpRequestModel : TransferBase {
        public TcpDataRequestModel Payload { get; set; }

        public TransferTcpRequestModel(TcpDataRequestModel payload, string id, DateTime created)
            : base(id, created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"tcp {base.ToString()}, Payload: '{Payload}'";
        }
    }
}
