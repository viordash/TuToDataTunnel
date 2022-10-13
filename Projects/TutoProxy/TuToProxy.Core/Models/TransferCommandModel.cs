
namespace TuToProxy.Core.Models {

    public class TransferUdpCommandModel : TransferBase {
        public UdpCommandModel Payload { get; set; }

        public TransferUdpCommandModel() : base(string.Empty, default) {
            Payload = new(0, 0, SocketCommand.Empty);
        }

        public TransferUdpCommandModel(TransferUdpRequestModel requestModel, UdpCommandModel payload)
            : base(requestModel.Id, requestModel.Created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"udp {base.ToString()}, Payload: '{Payload}'";
        }
    }

    public class TransferTcpCommandModel : TransferBase {
        public TcpCommandModel Payload { get; set; }

        public TransferTcpCommandModel() : base(string.Empty, default) {
            Payload = new(0, 0, SocketCommand.Empty);
        }

        public TransferTcpCommandModel(TransferTcpRequestModel requestModel, TcpCommandModel payload)
            : base(requestModel.Id, requestModel.Created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"tcp {base.ToString()}, Payload: '{Payload}'";
        }
    }
}
