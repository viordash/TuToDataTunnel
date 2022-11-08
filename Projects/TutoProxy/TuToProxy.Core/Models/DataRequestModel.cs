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


    public class TcpStreamDataModel : DataBaseModel {
        public Int64 Frame { get; set; }
        public TcpStreamDataModel(int port, int originPort, Int64 frame, byte[]? data) : base(port, originPort, data) {
            Frame = frame;
        }
        public override string ToString() {
            return $"tcp stream {Frame} {base.ToString()}";
        }
    }
}
