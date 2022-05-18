using TutoProxy.Core.Models;

namespace TuToProxy.Core.Models {
    public class DataResponseFactory {
        public static DataResponseModel Create(DataProtocol protocol, byte[] data) =>
            protocol switch {
                DataProtocol.Udp => new UdpDataResponseModel() { Data = data },
                DataProtocol.Tcp => new TcpDataResponseModel() { Data = data },
                _ => throw new NotImplementedException()
            };
    }
}
