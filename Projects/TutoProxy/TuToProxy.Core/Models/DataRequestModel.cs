using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public abstract class DataRequestModel : DataBaseModel {
        public DataProtocol Protocol { get; protected set; }

        public override string ToString() {
            return $"{base.ToString()}, prot:{Protocol}";
        }
    }

    public class UdpDataRequestModel : DataRequestModel {
        public UdpDataRequestModel() {
            Protocol = DataProtocol.Udp;
        }
    }

    public class TcpDataRequestModel : DataRequestModel {
        public TcpDataRequestModel() {
            Protocol = DataProtocol.Tcp;
        }
    }
}
