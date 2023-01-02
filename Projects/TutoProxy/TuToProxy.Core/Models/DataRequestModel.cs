using MessagePack;

namespace TuToProxy.Core.Models {

    [MessagePackFormatter(typeof(DataBaseModelFormatter<UdpDataRequestModel>))]
    public class UdpDataRequestModel : DataBaseModel {
        public override string ToString() {
            return $"udp request {base.ToString()}";
        }
    }

    [MessagePackFormatter(typeof(DataBaseModelFormatter<TcpDataRequestModel>))]
    public class TcpDataRequestModel : DataBaseModel {
        public override string ToString() {
            return $"tcp request {base.ToString()}";
        }
    }
}
