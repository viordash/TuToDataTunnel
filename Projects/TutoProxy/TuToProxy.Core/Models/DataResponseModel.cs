using MessagePack;

namespace TuToProxy.Core.Models {
    [MessagePackFormatter(typeof(DataBaseModelFormatter<UdpDataResponseModel>))]
    public class UdpDataResponseModel : DataBaseModel {
        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    [MessagePackFormatter(typeof(DataBaseModelFormatter<TcpDataResponseModel>))]
    public class TcpDataResponseModel : DataBaseModel {
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }
}
