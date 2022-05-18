using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class DataResponseModel : DataBaseModel {
        public DataProtocol Protocol { get; set; }

        public DataResponseModel() {
        }

        public override string ToString() {
            return $"{base.ToString()}, prot:{Protocol}";
        }
    }

    public class UdpDataResponseModel : DataResponseModel {
        public UdpDataResponseModel() {
            Protocol = DataProtocol.Udp;
        }
    }

    public class TcpDataResponseModel : DataResponseModel {
        public TcpDataResponseModel() {
            Protocol = DataProtocol.Tcp;
        }
    }
}
