using TuToProxy.Core.Models;

namespace TuToProxy.Core.Models {
    public class UdpDataResponseModel : DataBaseModel {
        public UdpDataResponseModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }

        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    public class TcpDataResponseModel : DataBaseModel {
        public TcpDataResponseModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
