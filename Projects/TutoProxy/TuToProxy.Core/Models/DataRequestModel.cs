using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public class UdpDataRequestModel : DataBaseModel {
        public bool FireNForget { get; set; } = false;

        public UdpDataRequestModel() {
        }
        public override string ToString() {
            return $"udp request {base.ToString()}, fnf:{FireNForget}";
        }
    }

    public class TcpDataRequestModel : DataBaseModel {
        public TcpDataRequestModel() {
        }
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
