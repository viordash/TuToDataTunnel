
namespace TuToProxy.Core.Models {
    public class UdpDataResponseModel : DataBaseModel {
        public UdpDataResponseModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }

        public override string ToString() {
            return $"udp response: {base.ToString()}";
        }
    }

    public class TcpDataResponseModel : DataBaseModel {
        public TcpDataResponseModel(int port, int originPort, byte[] data) : base(port, originPort, data) {
        }
        public override string ToString() {
            return $"tcp response: {base.ToString()}";
        }
    }

    public class TcpDataModel {
        public byte[] Data { get; set; }
        public TcpDataModel(byte[] data) {
            Data = data;
        }

        public override string ToString() => $"{Data.Length} b";
    }


}
