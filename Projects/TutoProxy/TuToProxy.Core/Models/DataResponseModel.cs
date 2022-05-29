using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class UdpDataResponseModel : DataBaseModel {
        public UdpDataResponseModel(int port, byte[] data) : base(port, data) {
        }

        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    public class TcpDataResponseModel : DataBaseModel {
        public TcpDataResponseModel(int port, byte[] data) : base(port, data) {
        }
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
