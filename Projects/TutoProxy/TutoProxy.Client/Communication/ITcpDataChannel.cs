using TuToProxy.Core.Models;

namespace TutoProxy.Client.Communication {
    public interface ITcpDataChannel {
        Task<int> SendTcpResponse(TcpDataResponseModel response, CancellationToken cancellationToken);
        Task<bool> DisconnectTcp(SocketAddressModel socketAddress, CancellationToken cancellationToken);
    }
}
