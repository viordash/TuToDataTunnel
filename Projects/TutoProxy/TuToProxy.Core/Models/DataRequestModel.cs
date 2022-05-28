using TuToProxy.Core.Models;

namespace TutoProxy.Core.Models {
    public abstract class DataRequestModel : DataBaseModel {
        public int Port { get; set; }

        public override string ToString() {
            return $"{base.ToString()}, port:{Port}";
        }
    }

    public class UdpDataRequestModel : DataRequestModel {
        public bool FireNForget { get; set; } = false;

        public UdpDataRequestModel() {
        }
        public override string ToString() {
            return $"udp request {base.ToString()}, fnf:{FireNForget}";
        }
    }

    public class TcpDataRequestModel : DataRequestModel {
        public TcpDataRequestModel() {
        }
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
