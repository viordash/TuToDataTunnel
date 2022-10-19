
namespace TuToProxy.Core.Models {

    public class TransferUdpCommandModel : TransferBase {
        public UdpCommandModel Payload { get; set; }

        public TransferUdpCommandModel() : base(string.Empty, default) {
            Payload = new(0, 0, SocketCommand.Empty);
        }

        public TransferUdpCommandModel(string id, DateTime created, UdpCommandModel payload)
            : base(id, created) {
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

        public TransferTcpCommandModel(string id, DateTime created, TcpCommandModel payload)
            : base(id, created) {
            Payload = payload;
        }

        public override string ToString() {
            return $"tcp {base.ToString()}, Payload: '{Payload}'";
        }
    }
}
