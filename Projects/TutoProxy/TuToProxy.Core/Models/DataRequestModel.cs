using TuToProxy.Core.Models;

namespace TuToProxy.Core.Models {
    public class UdpDataRequestModel : DataBaseModel {

        public UdpDataRequestModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }
        public override string ToString() {
            return $"udp request {base.ToString()}";
        }
    }

    public class TcpDataRequestModel : DataBaseModel {
        public TcpDataRequestModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }

    public class TcpStreamParam : SocketAddressModel {
        public TcpStreamParam(int port, int originPort) : base(port, originPort) {
        }
        public override string ToString() {
            return $"tcp stream {base.ToString()}";
        }
    }


    public class TcpStreamDataModel : DataBaseModel {
        public TcpStreamDataModel(int port, int originPort, byte[]? data) : base(port, originPort, data) {
        }
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
