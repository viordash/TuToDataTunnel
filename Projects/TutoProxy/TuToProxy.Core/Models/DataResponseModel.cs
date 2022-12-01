
using MessagePack;

namespace TuToProxy.Core.Models {
    [MessagePackObject]
    public class UdpDataResponseModel : DataBaseModel {
        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    [MessagePackObject]
    public class TcpDataResponseModel : DataBaseModel {
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
