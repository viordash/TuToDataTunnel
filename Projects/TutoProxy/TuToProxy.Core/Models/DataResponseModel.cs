using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class UdpDataResponseModel : DataBaseModel {
        public UdpDataResponseModel() : base() {
        }
        public UdpDataResponseModel(byte[] data) : this() {
            Data = data;
        }
        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    public class TcpDataResponseModel : DataBaseModel {
        public TcpDataResponseModel() {
        }
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
