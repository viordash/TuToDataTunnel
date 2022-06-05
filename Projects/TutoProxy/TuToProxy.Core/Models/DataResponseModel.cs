using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class UdpDataResponseModel : DataBaseModel {
        public UdpDataResponseModel(int port, int remotePort, byte[] data) : base(port, remotePort, data) {
        }

        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    public class TcpDataResponseModel : DataBaseModel {
        public TcpDataResponseModel(int port, int remotePort, byte[] data) : base(port, remotePort, data) {
        }
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
