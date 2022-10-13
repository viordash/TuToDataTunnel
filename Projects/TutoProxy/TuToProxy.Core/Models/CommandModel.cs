using TuToProxy.Core.Models;

namespace TuToProxy.Core.Models {

    public class UdpCommandModel : CommandBaseModel {
        public UdpCommandModel(int port, int originPort, SocketCommand command) : base(port, originPort, command) {
        }

        public override string ToString() {
            return $"udp command: {base.ToString()}";
        }
    }

    public class TcpCommandModel : CommandBaseModel {
        public TcpCommandModel(int port, int originPort, SocketCommand command) : base(port, originPort, command) {
        }
        public override string ToString() {
            return $"tcp command: {base.ToString()}";
        }
    }
}
