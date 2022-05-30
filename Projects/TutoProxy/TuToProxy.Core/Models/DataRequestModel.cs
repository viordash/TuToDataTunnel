using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class UdpDataRequestModel : DataBaseModel {

        public UdpDataRequestModel(int port, byte[] data) : base(port, data) {
        }
        public override string ToString() {
            return $"udp request {base.ToString()}";
        }
    }

    public class TcpDataRequestModel : DataBaseModel {
        public TcpDataRequestModel(int port, byte[] data) : base(port, data) {
        }
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
