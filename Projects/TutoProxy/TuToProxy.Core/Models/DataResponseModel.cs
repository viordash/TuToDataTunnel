using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public abstract class DataResponseModel : DataBaseModel {
        public DataProtocol? Protocol { get; set; }

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
