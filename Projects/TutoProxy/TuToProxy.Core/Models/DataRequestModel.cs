using MessagePack;

namespace TuToProxy.Core.Models {

    [MessagePackObject]
    public class UdpDataRequestModel : DataBaseModel {

        public override string ToString() {
            return $"udp request {base.ToString()}";
        }
    }

    [MessagePackObject]
    public class TcpDataRequestModel : DataBaseModel {

        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
